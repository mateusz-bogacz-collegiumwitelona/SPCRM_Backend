using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Email;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Tests.Email
{
    [NotInParallel]
    public class SmtpEmailServiceTest
    {
        private static IContainer _mailpitContainer = null!;
        private static string _mailpitHost = null!;
        private static string _mailpitPort = null!;

        [Before(Class)]
        [Obsolete]
        public static async Task SetupClassAsync()
        {
            _mailpitContainer = new ContainerBuilder()
                 .WithImage("axllent/mailpit:v1.29.4")
                 .WithPortBinding(1025, true)
                 .WithPortBinding(8025, true)
                 .WithWaitStrategy(Wait.ForUnixContainer()
                     .UntilMessageIsLogged(".*accessible via.*")
                     .UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v1/info")))
                 .Build();

            await _mailpitContainer.StartAsync();

            _mailpitHost = _mailpitContainer.Hostname;
            _mailpitPort = _mailpitContainer.GetMappedPublicPort(1025).ToString();
        }

        [After(Class)]
        public static async Task CleanupClassAsync()
        {
            await _mailpitContainer.DisposeAsync();
        }

        [Test]
        public async Task SendEmailAsync_WhenFromEmailIsMissing_LogsErrorAndAborts()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"Mail:Host", _mailpitHost},
                    {"Mail:Port", _mailpitPort},
                    {"Mail:EnableSsl", "false"}
                })
                .Build();

            var fakeLogger = new FakeLogger<SmtpEmailService>();
            var emailService = new SmtpEmailService(configuration, fakeLogger);

            // Act
            await emailService.SendEmailAsync("test@odbiorca.pl", "Temat", "Treść");

            // Assert 
            var expectedError = "An email cannot be sent. The (Mail:From) address is missing from the configuration.";

            await Assert.That(fakeLogger.LoggedMessages).Contains(expectedError);
        }

        [Test]
        public async Task SendEmailAsync_WithValidConfiguration_SendsEmailSuccessfully()
        {
            // Arrange
            Console.WriteLine($"[DEBUG SMTP] Mailpit Host: {_mailpitHost}, Port: {_mailpitPort}");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"Mail:Host", _mailpitHost},
                    {"Mail:Port", _mailpitPort},
                    {"Mail:EnableSsl", "false"},
                    {"Mail:From", "no-reply@spcrm.pl"},
                    {"Mail:DisplayName", "System SPCRM"}
                })
                .Build();

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
                builder.SetMinimumLevel(LogLevel.Trace);
            });

            var logger = loggerFactory.CreateLogger<SmtpEmailService>();
            var emailService = new SmtpEmailService(configuration, logger);

            // Act & Diagnose
            try
            {
                await emailService.SendEmailAsync(
                    "test_integracyjny@spcrm.pl",
                    "Test TUnit + Testcontainers",
                    "Wiadomość z testu"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("==================================================");
                Console.WriteLine("[DIAGNOSTIC ERROR] SmtpClient threw an exception:");
                Console.WriteLine($"Type: {ex.GetType().FullName}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Inner Exception: {ex.InnerException?.ToString() ?? "None"}");
                Console.WriteLine($"Full StackTrace:\n{ex}");

                try
                {
                    var (stdout, stderr) = await _mailpitContainer.GetLogsAsync();
                    Console.WriteLine("--- Mailpit Container StdOut ---");
                    Console.WriteLine(stdout);
                    Console.WriteLine("--- Mailpit Container StdErr ---");
                    Console.WriteLine(stderr);
                }
                catch (Exception logEx)
                {
                    Console.WriteLine($"Could not retrieve container logs: {logEx.Message}");
                }
                Console.WriteLine("==================================================");

                throw;
            }
        }
    }

    public class FakeLogger<T> : ILogger<T>
    {
        public ConcurrentBag<string> LoggedMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LoggedMessages.Add(formatter(state, exception));
        }
    }

    public class FakeBackgroundJobClient : IBackgroundJobClient
    {
        public bool JobEnqueued { get; private set; } = false;

        public string Create(Job job, IState state)
        {
            JobEnqueued = true;
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState)
        {
            return true;
        }
    }
}
