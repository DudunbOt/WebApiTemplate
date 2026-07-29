using ApplicationCore.Attributes;
using ApplicationCore.Interfaces;
using ApplicationCore.Interfaces.Base;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs
{
    [RecurringJob("process-email-queue", "*/5 * * * *", Queue = "emails")]
    public class EmailQueueJob : IHangfireJob
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailQueueJob> _logger;

        public EmailQueueJob(
            IEmailService emailService,
            ILogger<EmailQueueJob> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                _logger.LogInformation("Starting email queue processing");

                // Retry failed emails first
                var retriedCount = await _emailService.RetryFailedEmailsAsync();
                if (retriedCount > 0)
                {
                    _logger.LogInformation("Retried {Count} failed emails", retriedCount);
                }

                // Get pending emails
                var pendingEmails = await _emailService.GetPendingEmailsAsync(10);

                if (pendingEmails.Any())
                {
                    _logger.LogInformation("Found {Count} pending emails to process", pendingEmails.Count);

                    int sentCount = 0;
                    int failedCount = 0;

                    foreach (var email in pendingEmails)
                    {
                        var success = await _emailService.SendEmailAsync(email);
                        if (success)
                            sentCount++;
                        else
                            failedCount++;

                        // Small delay between emails to avoid overwhelming the SMTP server
                        await Task.Delay(1000);
                    }

                    _logger.LogInformation("Email processing complete. Sent: {SentCount}, Failed: {FailedCount}", sentCount, failedCount);
                }
                else
                {
                    _logger.LogDebug("No pending emails to process");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email queue processing");
                throw; // Rethrow so Hangfire can handle retries
            }
        }
    }
}
