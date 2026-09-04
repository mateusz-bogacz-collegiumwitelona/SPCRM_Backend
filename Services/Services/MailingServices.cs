using Domain.Common;
using Domain.Comunication;
using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.Command.Mailing;
using Services.Command.Support;
using Services.Helpers;
using Services.Interfaces;
using System.Globalization;

namespace Services.Services
{
    public class MailingServices : IMailingServices
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly string _supportEmail;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<MailingServices> _logger;

        public MailingServices(
            AppDbContext context,
            IConfiguration config,
            IEmailSender emailSender,
            ILogger<MailingServices> logger)
        {
            _context = context;
            _config = config;
            _supportEmail = _config["SUPPORT_EMAIL"]
                ?? throw new InvalidOperationException("Support email is not configured.");
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task<Result> SendEmailToSupport(SupportEmailCommand command)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == command.Email && !u.IsDeleted);

            if (user == null)
            {
                _logger.LogWarning("Support request rejected: User with email {Email} does not exist.", command.Email);
                return Result.Failure(
                    message: "User with the provided email does not exist.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            string formattedDate = DateTime.UtcNow.ToString(
                "dddd, dd MMMM yyyy HH:mm",
                new CultureInfo("pl-PL")
            );

            var reportDomain = new ReportDomain
            {
                SupportEmail = _supportEmail,
                UserName = user.FirstName,
                UserSurname = user.LastName,
                UserEmail = command.Email,
                Time = formattedDate,
                Title = command.Title,
                Message = command.Message,
            };

            await _emailSender.SendReportEmailAsync(reportDomain);

            _logger.LogInformation("Support email sent successfully from {UserEmail}.", command.Email);

            return Result.Success("Email sent to support successfully.", StatusCodes.Status200OK);
        }

        public async Task<Result> SendProductMailingAsync(MailingCommand command, Guid authorId)
        {
            var authorExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == authorId && !u.IsDeleted);

            if (!authorExists)
            {
                _logger.LogError("Security/Integrity violation: Non-existent user {AuthorId} tried to dispatch mailing.", authorId);
                throw new UserNotFoundException(authorId);
            }

            var (existingClients, clientError) = await GetAndValidateClientsAsync(command.To);
            if (clientError != null) return clientError;

            var clientsWithoutEmail = existingClients
                .Where(c => !c.ContactDetails.Any(cd => !cd.IsDeleted && cd.Type == ContactDetailTypeEnum.EMAIL && !string.IsNullOrWhiteSpace(cd.Value)))
                .Select(c => $"{c.FirstName} {c.LastName} ({c.Id})")
                .ToList();

            if (clientsWithoutEmail.Any())
            {
                _logger.LogWarning("Mailing aborted: Following contacts lack a valid email: {Clients}", string.Join(", ", clientsWithoutEmail));
                return Result.Failure(
                    message: "One or more selected contacts do not have an active email address.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation,
                    errors: clientsWithoutEmail
                );
            }

            var (existingProducts, productError) = await GetAndValidateProductsAsync(command.Products);
            if (productError != null) return productError;

            var currencies = await _context.Currencies
                .AsNoTracking()
                .ToListAsync();

            if (!currencies.Any())
            {
                _logger.LogError("Critical data corruption: Currency dictionary is completely empty.");
                throw new DataCorruptionException("No currencies configured in the system.");
            }

            var productsToOffer = PrepareProductsToOffer(command.Products, existingProducts, currencies);

            await CreateAndSaveOffersAsync(existingClients, productsToOffer, authorId);

            await DispatchMailingAsync(existingClients, productsToOffer, command.Language);

            _logger.LogInformation("Product mailing successfully sent to {ClientCount} clients by user {AuthorId}.", existingClients.Count, authorId);

            return Result.Success(
                message: "Product mailing sent and offers recorded successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        private async Task<(List<Contact> Contacts, Result? Error)> GetAndValidateClientsAsync(IEnumerable<Guid> clientIds)
        {
            var groupedClients = clientIds.Distinct().ToList();

            var existingClients = await _context.Contacts
                .Include(c => c.ContactDetails)
                .Where(u => groupedClients.Contains(u.Id))
                .ToListAsync();

            var missingClients = groupedClients.Except(existingClients.Select(u => u.Id)).ToList();

            if (missingClients.Any())
            {
                _logger.LogInformation("Mailing validation failed: Clients do not exist: {MissingClients}", string.Join(", ", missingClients));
                var error = Result.Failure(
                    message: "Some clients do not exist.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ClientNotFound,
                    errors: missingClients.Select(id => $"Client with ID {id} does not exist.").ToList()
                );
                return (new List<Contact>(), error);
            }

            var corruptedContact = existingClients.FirstOrDefault(c => c.CompanyId == Guid.Empty || c.OwnerId == Guid.Empty);
            if (corruptedContact != null)
            {
                _logger.LogError("Critical data corruption: Contact {ContactId} has corrupted company or owner.", corruptedContact.Id);
                throw new DataCorruptionException($"Contact '{corruptedContact.Id}' has missing company or owner relation.");
            }

            return (existingClients, null);
        }

        private async Task<(List<Product> Products, Result? Error)> GetAndValidateProductsAsync(IEnumerable<MailingProductCommand> productCommands)
        {
            var groupedProducts = productCommands.Select(p => p.ProductId).Distinct().ToList();

            var existingProducts = await _context.Products
                .Include(p => p.Unit)
                .Include(p => p.SteelGrade)
                .Include(p => p.Currency)
                .Include(p => p.Promotions.Where(pr =>
                    pr.IsActive &&
                    (!pr.StartDate.HasValue || pr.StartDate <= DateTime.UtcNow) &&
                    (!pr.EndDate.HasValue || pr.EndDate >= DateTime.UtcNow)))
                .Where(p => groupedProducts.Contains(p.Id))
                .ToListAsync();

            var missingProducts = groupedProducts.Except(existingProducts.Select(p => p.Id)).ToList();

            if (missingProducts.Any())
            {
                _logger.LogInformation("Mailing validation failed: Products do not exist: {MissingProducts}", string.Join(", ", missingProducts));
                var error = Result.Failure(
                    message: "Some products do not exist.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.ProductNotFound,
                    errors: missingProducts.Select(id => $"Product with ID {id} does not exist.").ToList()
                );
                return (new List<Product>(), error);
            }

            var corruptedProduct = existingProducts.FirstOrDefault(p => p.PricePerUnit < 0 || p.CurrencyId == Guid.Empty);
            if (corruptedProduct != null)
            {
                _logger.LogError("Critical data corruption: Product {ProductId} has negative price or missing currency relation.", corruptedProduct.Id);
                throw new DataCorruptionException($"Product '{corruptedProduct.Id}' has corrupted pricing or currency state.");
            }

            return (existingProducts, null);
        }

        private List<MailingProductItemDomain> PrepareProductsToOffer(
            IEnumerable<MailingProductCommand> commands,
            List<Product> existingProducts,
            List<Currency> currencies)
        {
            var uniqueCommands = commands.GroupBy(p => p.ProductId).Select(g => g.First()).ToList();
            var defaultCurrency = currencies.FirstOrDefault(c => c.Code == "PLN") ?? currencies.First();

            return uniqueCommands.Select(cmd =>
            {
                var product = existingProducts.First(p => p.Id == cmd.ProductId);

                var targetCurrency = !string.IsNullOrWhiteSpace(cmd.CurrencyCode)
                    ? currencies.FirstOrDefault(c => c.Code.Equals(cmd.CurrencyCode, StringComparison.OrdinalIgnoreCase)) ?? defaultCurrency
                    : product.Currency ?? defaultCurrency;

                var formatDimension = DimensionsFormatter.Format(
                    product.Category, product.Diameter, product.Thickness, product.Width, product.Length);

                bool isSameCurrency = product.CurrencyId == targetCurrency.Id;
                long standardPrice = product.PricePerUnit;
                long finalPrice = cmd.Price ?? standardPrice;

                decimal? discountPercentage = null;
                bool isPromoted = false;
                long? originalPrice = null;

                if (isSameCurrency)
                {
                    if (finalPrice < standardPrice)
                    {
                        isPromoted = true;
                        originalPrice = standardPrice;
                        discountPercentage = Math.Round((1m - ((decimal)finalPrice / standardPrice)) * 100m, 2);
                    }

                    var activePromotion = product.Promotions
                        .Where(pr =>
                            !pr.ContactId.HasValue &&
                            (!pr.MinQuantity.HasValue || cmd.Quantity >= pr.MinQuantity.Value))
                        .OrderByDescending(pr => pr.DiscountPercentage ?? 0)
                        .FirstOrDefault();

                    if (activePromotion != null && !cmd.Price.HasValue)
                    {
                        if (activePromotion.PromotionalPrice.HasValue && activePromotion.CurrencyId == targetCurrency.Id)
                        {
                            isPromoted = true;
                            originalPrice = standardPrice;
                            finalPrice = activePromotion.PromotionalPrice.Value;
                            discountPercentage = Math.Round((1m - ((decimal)finalPrice / standardPrice)) * 100m, 2);
                        }
                        else if (activePromotion.DiscountPercentage.HasValue)
                        {
                            isPromoted = true;
                            originalPrice = standardPrice;
                            finalPrice = (long)(standardPrice * (1m - (activePromotion.DiscountPercentage.Value / 100m)));
                            discountPercentage = activePromotion.DiscountPercentage.Value;
                        }
                    }
                }
                else
                {
                    originalPrice = null;
                    discountPercentage = null;
                    isPromoted = false;
                }

                return new MailingProductItemDomain
                {
                    ProductId = product.Id,
                    CurrencyId = targetCurrency.Id,
                    ProductName = product.Name,
                    SteelGrade = product.SteelGrade?.Name ?? string.Empty,
                    FormattedDimensions = formatDimension,
                    Weight = product.Weight,
                    UnitSymbol = product.Unit?.Symbol ?? "szt.",
                    Quantity = cmd.Quantity,
                    CurrencyCode = targetCurrency.Code,
                    FinalPrice = finalPrice,
                    OriginalPrice = originalPrice,
                    DiscountPercentage = discountPercentage,
                    IsPromoted = isPromoted
                };
            }).ToList();
        }

        private async Task CreateAndSaveOffersAsync(
            List<Contact> clients,
            List<MailingProductItemDomain> productsToOffer,
            Guid authorId)
        {
            var offerCurrencyId = productsToOffer.FirstOrDefault()?.CurrencyId
                ?? (await _context.Currencies.FirstAsync(c => c.Code == "PLN")).Id;

            foreach (var client in clients)
            {
                var newOffer = new Offer
                {
                    Id = Guid.NewGuid(),
                    Name = $"OF/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                    ContactId = client.Id,
                    CreatedByUserId = authorId,
                    CurrencyId = offerCurrencyId,
                    ValidUntil = DateTime.UtcNow.AddDays(7),
                    Status = OfferStatusEnum.Sent,
                    Products = productsToOffer.Select(p => new OfferProducts
                    {
                        Id = Guid.NewGuid(),
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,
                        QuotedPrice = p.FinalPrice,
                    }).ToList()
                };

                _context.Offers.Add(newOffer);
            }

            await _context.SaveChangesAsync();
        }

        private async Task DispatchMailingAsync(
            List<Contact> clients,
            List<MailingProductItemDomain> productsToOffer,
            string language)
        {
            var emailsToSend = clients
                .SelectMany(c => c.ContactDetails)
                .Where(cd => !cd.IsDeleted && cd.Type == ContactDetailTypeEnum.EMAIL)
                .Select(cd => cd.Value.Trim())
                .Where(email => !string.IsNullOrEmpty(email))
                .Distinct()
                .ToList();

            var offerDomain = new MailingOfferDomain
            {
                BccEmails = emailsToSend,
                Language = language,
                Products = productsToOffer
            };

            await _emailSender.SendProductMailingAsync(offerDomain);
        }
    }
}
