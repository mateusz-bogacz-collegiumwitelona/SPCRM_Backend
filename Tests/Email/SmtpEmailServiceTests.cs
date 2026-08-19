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
                .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(1025))
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

            var realLogger = new LoggerFactory().CreateLogger<SmtpEmailService>();
            var emailService = new SmtpEmailService(configuration, realLogger);

            // Act
            Func<Task> action = async () => await emailService.SendEmailAsync(
                "test_integracyjny@spcrm.pl",
                "Test TUnit + Testcontainers",
                "Wiadomość z testu"
            );

            // Assert 
            await Assert.That(action).ThrowsNothing();
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
