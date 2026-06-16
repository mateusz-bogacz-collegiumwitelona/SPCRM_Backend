using Domain.Common;
using Domain.Constants;
using DTO.Domain;
using DTO.Request;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.Interfaces;
using System.Globalization;

namespace Services.Services
{
    public class SupportServices : ISupportServices
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly string _supportEmail;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<SupportServices> _logger;

        public SupportServices(AppDbContext context,
            IConfiguration config,
            IEmailSender emailSender,
            ILogger<SupportServices> logger
            )
        {
            _context = context;
            _config = config;
            _supportEmail = _config["SUPPORT_EMAIL"]
                ?? throw new InvalidOperationException("Support email is not configured.");
            _emailSender = emailSender;

            _logger = logger;
        }

        public async Task<Result> SendEmailToSupport(SupportEmailRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                _logger.LogError("User with email {email} doesn't exist.", request.Email);
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
                UserEmail = request.Email,
                Time = date,
                Title = request.Title,
                Message = request.Message,
            };

            await _emailSender.SendReportEmailAsync(domain);

            return Result.Success("Email sent to support successfully.", StatusCodes.Status200OK);
        }
    }
}
