using Domain.Common;
using Domain.Comunication;
using Domain.Constants;
using Domain.Enum;
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

        public MailingServices(AppDbContext context,
            IConfiguration config,
            IEmailSender emailSender,
            ILogger<MailingServices> logger
            )
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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == command.Email);

            if (user == null)
            {
                _logger.LogError("User with email {email} doesn't exist.", command.Email);
                return Result.Failure(
                    "User with the provided email does not exist.",
                    ErrorCodes.UserNotFound,
                    StatusCodes.Status404NotFound
                    );
            }

            string date = DateTime.UtcNow.ToString(
                "dddd, dd MMMM yyyy HH:mm",
                new CultureInfo("pl-PL")
                );

            var domain = new ReportDomain
            {
                SupportEmail = _supportEmail,
                UserName = user.FirstName,
                UserSurname = user.LastName,
                UserEmail = command.Email,
                Time = date,
                Title = command.Title,
                Message = command.Message,
            };

            await _emailSender.SendReportEmailAsync(domain);

            return Result.Success("Email sent to support successfully.", StatusCodes.Status200OK);
        }

        public async Task<Result> SendProductMailingAsync(MailingCommand command, Guid authorId)
        {
            var (existingClients, clientError) = await GetAndValidateClientsAsync(command.To);
            if (clientError != null) return clientError;

            var (existingProducts, productError) = await GetAndValidateProductsAsync(command.Products);
            if (productError != null) return productError;

            var currencyId = await GetCurrencyIdAsync(command.Products.FirstOrDefault()?.CurrencyCode);

            var productsToOffer = PrepareProductsToOffer(command.Products, existingProducts);

            await CreateAndSaveOffersAsync(existingClients, existingProducts, productsToOffer, currencyId, authorId);

            await DispatchMailingAsync(existingClients, productsToOffer, command.Language);

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
                _logger.LogError("Some clients do not exist: {missingClients}", string.Join(", ", missingClients));
                var error = Result.Failure(
                    "Some clients do not exist.",
                    ErrorCodes.ClientNotFound,
                    StatusCodes.Status404NotFound,
                    missingClients.Select(id => $"Client with ID {id} does not exist.").ToList()
                );
                return (new List<Contact>(), error);
            }

            return (existingClients, null);
        }

        private async Task<(List<Product> Products, Result? Error)> GetAndValidateProductsAsync(IEnumerable<MailingProductCommand> productCommands)
        {
            var groupedProducts = productCommands.Select(p => p.ProductId).Distinct().ToList();

            var existingProducts = await _context.Products
                .Include(p => p.Unit)
                .Include(p => p.Promotions.Where(pr =>
                    pr.IsActive &&
                    (!pr.StartDate.HasValue || pr.StartDate <= DateTime.UtcNow) &&
                    (!pr.EndDate.HasValue || pr.EndDate >= DateTime.UtcNow)))
                .Where(p => groupedProducts.Contains(p.Id))
                .ToListAsync();

            var missingProducts = groupedProducts.Except(existingProducts.Select(p => p.Id)).ToList();

            if (missingProducts.Any())
            {
                _logger.LogError("Some products do not exist: {missingProducts}", string.Join(", ", missingProducts));
                var error = Result.Failure(
                    "Some products do not exist.",
                    ErrorCodes.ProductNotFound,
                    StatusCodes.Status404NotFound,
                    missingProducts.Select(id => $"Product with ID {id} does not exist.").ToList()
                );
                return (new List<Product>(), error);
            }

            return (existingProducts, null);
        }

        private async Task<Guid> GetCurrencyIdAsync(string? currencyCode)
        {
            string code = currencyCode ?? "PLN";
            var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == code)
                           ?? await _context.Currencies.FirstAsync();

            return currency.Id;
        }

        private List<MailingProductItemDomain> PrepareProductsToOffer(
            IEnumerable<MailingProductCommand> commands,
            List<Product> existingProducts
        )
        {
            var uniqueCommands = commands.GroupBy(p => p.ProductId).Select(g => g.First()).ToList();

            return uniqueCommands.Select(cmd =>
            {
                var product = existingProducts.First(p => p.Id == cmd.ProductId);

                var formatDimmension = DimensionsFormatter.Format(
                    product.Category, product.Diameter, product.Thickness, product.Width, product.Length);

                long standardPrice = product.PricePerUnit;
                long finalPrice = cmd.Price ?? standardPrice;

                decimal? discountPercentage = null;
                bool isPromoted = false;
                long? originalPrice = null;

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

                if (activePromotion != null)
                {
                    isPromoted = true;
                    originalPrice = standardPrice;

                    if (activePromotion.PromotionalPrice.HasValue)
                    {
                        finalPrice = activePromotion.PromotionalPrice.Value;
                        discountPercentage = Math.Round((1m - ((decimal)finalPrice / standardPrice)) * 100m, 2);
                    }
                    else if (activePromotion.DiscountPercentage.HasValue)
                    {
                        finalPrice = (long)(standardPrice * (1m - (activePromotion.DiscountPercentage.Value / 100m)));
                        discountPercentage = activePromotion.DiscountPercentage.Value;
                    }
                }

                return new MailingProductItemDomain
                {
                    ProductName = product.Name,
                    SteelGrade = product.SteelGrade.Name,
                    FormattedDimensions = formatDimmension,
                    Weight = product.Weight,
                    UnitSymbol = product.Unit?.Symbol ?? "szt.",
                    Quantity = cmd.Quantity,
                    CurrencyCode = cmd.CurrencyCode ?? "PLN",
                    FinalPrice = finalPrice,
                    OriginalPrice = originalPrice,
                    DiscountPercentage = discountPercentage,
                    IsPromoted = isPromoted
                };
            }).ToList();
        }

        private async Task CreateAndSaveOffersAsync(
            List<Contact> clients,
            List<Product> existingProducts,
            List<MailingProductItemDomain> productsToOffer,
            Guid currencyId,
            Guid authorId)
        {
            foreach (var client in clients)
            {
                var newOffer = new Offer
                {
                    Id = Guid.NewGuid(),
                    ContactId = client.Id,
                    CreatedByUserId = authorId,
                    ValidUntil = DateTime.UtcNow.AddDays(7),
                    Status = OfferStatusEnum.Sent,
                    Products = productsToOffer.Select(p => new OfferProducts
                    {
                        Id = Guid.NewGuid(),
                        ProductId = existingProducts.First(ep => ep.Name == p.ProductName).Id,
                        Quantity = p.Quantity,
                        QuotedPrice = p.FinalPrice,
                        CurrencyId = currencyId,
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
                .Where(cd => cd.Type == ContactDetailTypeEnum.EMAIL && cd.IsPrimary)
                .Select(cd => cd.Value)
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
