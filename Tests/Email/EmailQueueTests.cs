using Email;

namespace Tests.Email
{
    public class EmailQueueTests
    {
        [Test]
        public async Task Queue_EnqueuesAndDequeuesSuccessfully()
        {
            var queue = new EmailQueue();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            queue.QueueEmail("jan@test.pl", "Test Subject", "Test Body");
            var dequeuedEmail = await queue.DequeueAsync(cts.Token);

            await Assert.That(dequeuedEmail.To).IsEquatableTo("jan@test.pl");
            await Assert.That(dequeuedEmail.Subject).IsEquatableTo("Test Subject");
            await Assert.That(dequeuedEmail.Body).IsEquatableTo("Test Body");
        }
    }
}
