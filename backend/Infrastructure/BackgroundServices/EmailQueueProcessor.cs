using ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundServices
{
    public class EmailQueueProcessor : BackgroundService
    {
        private readonly ILogger<EmailQueueProcessor> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(5);

        public EmailQueueProcessor(
            ILogger<EmailQueueProcessor> _logger,
            IServiceProvider serviceProvider)
        {
            this._logger = _logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email Queue Processor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessEmailQueueAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing email queue");
                }

                await Task.Delay(_processingInterval, stoppingToken);
            }

            _logger.LogInformation("Email Queue Processor stopped");
        }

        private async Task ProcessEmailQueueAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            try
            {
                _logger.LogInformation("Starting email queue processing");

                // Retry failed emails first
                var retriedCount = await emailService.RetryFailedEmailsAsync(cancellationToken);
                if (retriedCount > 0)
                {
                    _logger.LogInformation("Retried {Count} failed emails", retriedCount);
                }

                // Get pending emails
                var pendingEmails = await emailService.GetPendingEmailsAsync(10, cancellationToken);

                if (pendingEmails.Any())
                {
                    _logger.LogInformation("Found {Count} pending emails to process", pendingEmails.Count);

                    int sentCount = 0;
                    int failedCount = 0;

                    foreach (var email in pendingEmails)
                    {
                        var success = await emailService.SendEmailAsync(email, cancellationToken);
                        if (success)
                            sentCount++;
                        else
                            failedCount++;

                        // Small delay between emails to avoid overwhelming the SMTP server
                        await Task.Delay(1000, cancellationToken);
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
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Email Queue Processor is stopping");
            return base.StopAsync(cancellationToken);
        }
    }
}
