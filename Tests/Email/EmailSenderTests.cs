using DTO.Domain;
using Email;
using Email.Interfaces;
using Microsoft.Extensions.Logging;

namespace Tests.Email
{
    [NotInParallel]
    public class EmailSenderTests
    {
        private string _templateDirectory = null!;
        private string _templatePath = null!;

        [Before(Test)]
        public void Setup()
        {
            _templateDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
            Directory.CreateDirectory(_templateDirectory);
            _templatePath = Path.Combine(_templateDirectory, "report.html");

            string fakeHtmlTemplate = "Witaj {{Name}} {{Surname}}!" +
                " Twój email to {{Email}}." +
                " Czas: {{Time}}. " +
                "Tytuł: {{Title}}. " +
                "Wiadomość: {{Message}}.";

            File.WriteAllText(_templatePath, fakeHtmlTemplate);
        }

        [After(Test)]
        public void Cleanup()
        {
            if (Directory.Exists(_templateDirectory))
            {
                Directory.Delete(_templateDirectory, true);
            }
        }

        [Test]
        public async Task SendReportEmailAsync_WhenTemplateExists_ReplacesTagsAndQueuesEmail()
        {
            // Arrange
            var fakeQueue = new FakeEmailQueue();
            var logger = new LoggerFactory().CreateLogger<EmailSender>();
            var sender = new EmailSender(logger, fakeQueue);

            var report = new ReportDomain
            {
                UserName = "Jan",
                UserSurname = "Kowalski",
                UserEmail = "jan@test.pl",
                Time = "12:00",
                Title = "Błąd systemu",
                Message = "Nie działa",
                SupportEmail = "support@firma.pl"
            };

            // Act
            await sender.SendReportEmailAsync(report);

            // Assert
            await Assert.That(fakeQueue.QueuedEmails).HasCount().EqualTo(1);

            var queuedEmail = fakeQueue.QueuedEmails.First();

            await Assert.That(queuedEmail.To).IsEqualTo("support@firma.pl");
            await Assert.That(queuedEmail.Subject).Contains("Jan Kowalski");
            var expectedBody = "Witaj Jan Kowalski! " +
                "Twój email to jan@test.pl. " +
                "Czas: 12:00. " +
                "Tytuł: Błąd systemu. " +
                "Wiadomość: Nie działa.";

            await Assert.That(queuedEmail.Body).IsEqualTo(expectedBody);
        }

        [Test]
        public async Task SendReportEmailAsync_WhenTemplateIsMissing_CatchesExceptionAndDoesNotQueue()
        {
            // Arrange
            File.Delete(_templatePath);

            var fakeQueue = new FakeEmailQueue();
            var logger = new LoggerFactory().CreateLogger<EmailSender>();
            var sender = new EmailSender(logger, fakeQueue);

            var report = new ReportDomain
            {
                SupportEmail = "support@firma.pl",
                UserName = "Jan",
                UserSurname = "Kowalski",
                UserEmail = "jan@test.pl",
                Time = "12:00",
                Title = "Błąd systemu",
                Message = "Nie działa"
            };

            // Act
            await sender.SendReportEmailAsync(report);

            // Assert
            await Assert.That(fakeQueue.QueuedEmails).IsEmpty();
        }
    }

    public class FakeEmailQueue : IEmailQueue
    {
        public List<(string To, string Subject, string Body)> QueuedEmails { get; } = new();

        public void QueueEmail(string to, string subject, string body)
        {
            QueuedEmails.Add((to, subject, body));
        }

        public ValueTask<EmailDomain> DequeueAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
