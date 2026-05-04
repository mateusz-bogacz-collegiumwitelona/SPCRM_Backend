using Domain;
using Domain.Common;
using DTO.Domain;
using DTO.Request;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Timers;

namespace Services.Services
{
    public class SupportServices : ISupportServices
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly string _supportEmail;
        private readonly IEmailSender _emailSender;
        public SupportServices(AppDbContext context,
            IConfiguration config,
            IEmailSender emailSender
            )
        {
            _context = context;
            _config = config;
            _supportEmail = _config["SUPPORT_EMAIL"]
                ?? throw new InvalidOperationException("Support email is not configured.");
            _emailSender = emailSender;
        }

        public async Task<Result> SendEmailToSupport(SupportEmailRequest request)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null)
                {
                    return Result.Failure("User with the provided email does not exist.", StatusCodes.Status404NotFound);
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
            catch (Exception ex)
            {
                return Result.Failure(
                    "An error occurred while sending the email to support.",
                    StatusCodes.Status500InternalServerError,
                    new List<string> { ex.Message }
                    );
            }
        }
    }
}
