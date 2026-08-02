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
using Services.Command;
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
            var grouptClients = command.To
                .GroupBy(x => x)
                .Select(g => g.First())
                .ToList();

            var existingClients = await _context.Contacts
                .Include(c => c.ContactDetails)
                .Where(u => grouptClients.Contains(u.Id))
                .ToListAsync();

            var missingClients = grouptClients
                .Except(existingClients.Select(u => u.Id))
                .ToList();

            if (missingClients.Any())
            {
                _logger.LogError("Some clients do not exist: {missingClients}", string.Join(", ", missingClients));

                return Result.Failure(
                    "Some clients do not exist.",
                    ErrorCodes.ClientNotFound,
                    StatusCodes.Status404NotFound,
                    missingClients.Select(id => $"Client with ID {id} does not exist.").ToList()
                );
            }

            var groupProducts = command.Products
                .GroupBy(p => p.ProductId)
                .Select(g => g.First())
                .ToList();

            var existingProducts = await _context.Products
                .Include(p => p.Unit)
                .Include(p => p.Promotions.Where(pr =>
                    pr.IsActive &&
                    (!pr.StartDate.HasValue || pr.StartDate <= DateTime.UtcNow) &&
                    (!pr.EndDate.HasValue || pr.EndDate >= DateTime.UtcNow)))
                .Where(p => groupProducts.Select(x => x.ProductId).Contains(p.Id))
                .ToListAsync();

            var missingProducts = groupProducts
                .Select(x => x.ProductId)
                .Except(existingProducts.Select(p => p.Id))
                .ToList();

            if (missingProducts.Any())
            {
                _logger.LogError("Some products do not exist: {missingProducts}", string.Join(", ", missingProducts));
                return Result.Failure(
                    "Some products do not exist.",
                    ErrorCodes.ProductNotFound,
                    StatusCodes.Status404NotFound,
                    missingProducts.Select(id => $"Product with ID {id} does not exist.").ToList()
                );
            }

            string defaultCurrencyCode = groupProducts.FirstOrDefault()?.CurrencyCode ?? "PLN";
            var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == defaultCurrencyCode)
                           ?? await _context.Currencies.FirstAsync();


            var productsToOffer = groupProducts.Select(cmd =>
            {
                var product = existingProducts.First(p => p.Id == cmd.ProductId);

                var formatDimmension = DimensionsFormatter.Format(
                    product.Category,
                    product.Diameter,
                    product.Thickness,
                    product.Width,
                    product.Length
                );

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

                var activePromotion = product.Promotions.FirstOrDefault();
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
                    SteelGrade = product.SteelGrade,
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

            foreach (var client in existingClients)
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
                        CurrencyId = currency.Id, 
                    }).ToList()
                };

                _context.Offers.Add(newOffer);
            }

            await _context.SaveChangesAsync();

            var emailToSend = existingClients
                .SelectMany(c => c.ContactDetails)
                .Where(cd => cd.Type == ContactDetailTypeEnum.EMAIL && cd.IsPrimary)
                .Select(cd => cd.Value)
                .Distinct()
                .ToList();

            var offerDomain = new MailingOfferDomain
            {
                BccEmails = emailToSend,
                Language = command.Language,
                Products = productsToOffer
            };

            await _emailSender.SendProductMailingAsync(offerDomain);

            return Result.Success(
                message: "Product mailing sent and offers recorded successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
