using Domain.Constants;
using Domain.Models;
using DTO.Domain;
using DTO.Request;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Interfaces;
using Services.Services;
using System;
using System.Collections.Generic;
using System.Text;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class SupportServicesTest
    {

        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected SupportServices _supportServicesMock = null!;
        protected ILogger<SupportServices> _loggerMock = null!;
        protected FakeEmailSender _fakeEmailSender = null!;

        [Before(Class)]
        [Obsolete]
        public static async Task SetupClassAsync()
        {
            _dbContainer = new PostgreSqlBuilder()
                .WithImage("postgis/postgis:18-3.6")
                .WithDatabase("testdb")
                .WithUsername("testuser")
                .WithPassword("testpassword")
                .Build();

            await _dbContainer.StartAsync();

            _connectionString = _dbContainer.GetConnectionString();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString, options =>
                {
                    options.UseNetTopologySuite();
                })
                .Options;
            using var context = new AppDbContext(dbOptions);
            await context.Database.EnsureCreatedAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
        }

        [After(Class)]
        public static async Task CleanupClassAsync()
            => await _dbContainer.DisposeAsync();

        [Before(Test)]
        public async Task SetupAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString, options =>
                {
                    options.UseNetTopologySuite();
                })
                .Options;

            _contextMock = new AppDbContext(dbOptions);
            await _contextMock.Database.EnsureCreatedAsync();


            _loggerMock = new LoggerFactory().CreateLogger<SupportServices>();
            _fakeEmailSender = new FakeEmailSender();

            var inMemorySettings = new Dictionary<string, string> {
                {"SUPPORT_EMAIL", "support@mojafirma.pl"}
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();

            _supportServicesMock = new SupportServices(
                _contextMock, 
                configuration, 
                _fakeEmailSender, 
                _loggerMock
            );
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            if (_contextMock != null)
            {
                await _contextMock.DisposeAsync();
            }
        }

        // ─── SendEmailToSupport ─────────────────────────────────────────────────

        [Test]
        public async Task SendEmailToSupport_WhenUserDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var request = new SupportEmailRequest
            {
                Email = "nonexistent@test.pl",
                Title = "Problem",
                Message = "Nie działa"
            };

            // Act
            var result = await _supportServicesMock.SendEmailToSupport(request);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCodes.UserNotFound);
            await Assert.That(_fakeEmailSender.CallCount).IsEqualTo(0);
        }

        [Test]
        public async Task SendEmailToSupport_WhenUserExists_BuildsDomainAndPassesToSender()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var email = $"user_{uniqueSuffix}@test.pl";

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = email,
                NormalizedEmail = email.ToUpper(),
                FirstName = "Adam",
                LastName = "Kowalski"
            };

            _contextMock.Users.Add(user);
            await _contextMock.SaveChangesAsync();

            var request = new SupportEmailRequest
            {
                Email = email,
                Title = "Błąd w module X",
                Message = "Krótki opis błędu"
            };

            // Act
            var result = await _supportServicesMock.SendEmailToSupport(request);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(_fakeEmailSender.CallCount).IsEqualTo(1);
            await Assert.That(_fakeEmailSender.SentReport).IsNotNull();

            var sentReport = _fakeEmailSender.SentReport!;

            await Assert.That(sentReport.UserEmail).IsEqualTo(email);
            await Assert.That(sentReport.UserName).IsEqualTo("Adam");
            await Assert.That(sentReport.UserSurname).IsEqualTo("Kowalski");
            await Assert.That(sentReport.Title).IsEqualTo("Błąd w module X");
            await Assert.That(sentReport.SupportEmail).IsEqualTo("support@mojafirma.pl");
            await Assert.That(sentReport.Time).IsNotNull();
        }
    }

    public class FakeEmailSender : IEmailSender
    {
        public ReportDomain? SentReport { get; private set; }
        public int CallCount { get; private set; } = 0;

        public Task SendReportEmailAsync(ReportDomain report)
        {
            SentReport = report;
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
