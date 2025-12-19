using ApplicationCore.Entities;
using ApplicationCore.Entities.Base;
using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Configurations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace Infrastructure.Services
{
    public class EmailService : ServiceBase<EmailQueue>, IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            AppDbContext context,
            IDistributedCache cache,
            IOptions<AppConfig> appConfig,
            IOptions<EmailSettings> emailSettings,
            ILogger<EmailService> logger)
            : base(context, cache, appConfig)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
        }

        #region Queue Operations

        public async Task<EmailQueue> QueueEmailAsync(EmailMessage emailMessage, DateTime? scheduledAt = null, CancellationToken cancellationToken = default)
        {
            var emailQueue = new EmailQueue
            {
                To = JsonSerializer.Serialize(emailMessage.To),
                Cc = emailMessage.Cc != null ? JsonSerializer.Serialize(emailMessage.Cc) : null,
                Bcc = emailMessage.Bcc != null ? JsonSerializer.Serialize(emailMessage.Bcc) : null,
                Subject = emailMessage.Subject,
                Body = emailMessage.Body,
                IsHtml = emailMessage.IsHtml,
                Attachments = emailMessage.Attachments != null ? JsonSerializer.Serialize(emailMessage.Attachments) : null,
                ReplyTo = emailMessage.ReplyTo != null ? JsonSerializer.Serialize(emailMessage.ReplyTo) : null,
                Status = "Pending",
                ScheduledAt = scheduledAt ?? DateTime.Now,
                CreatedDate = DateTime.Now,
                UpdatedDate = DateTime.Now
            };

            emailQueue = await Upsert(emailQueue, 0, cancellationToken);
            _logger.LogInformation("Email queued successfully with ID: {EmailId}", emailQueue.Id);

            return emailQueue;
        }

        public async Task<EmailQueue> QueueSimpleEmailAsync(string toEmail, string subject, string body, bool isHtml = true, DateTime? scheduledAt = null, CancellationToken cancellationToken = default)
        {
            var emailMessage = new EmailMessage
            {
                To = new List<EmailAddress> { new EmailAddress(toEmail) },
                Subject = subject,
                Body = body,
                IsHtml = isHtml
            };

            return await QueueEmailAsync(emailMessage, scheduledAt, cancellationToken);
        }

        public async Task<EmailQueue> QueueBulkEmailAsync(List<string> toEmails, string subject, string body, bool isHtml = true, DateTime? scheduledAt = null, CancellationToken cancellationToken = default)
        {
            var emailMessage = new EmailMessage
            {
                To = toEmails.Select(email => new EmailAddress(email)).ToList(),
                Subject = subject,
                Body = body,
                IsHtml = isHtml
            };

            return await QueueEmailAsync(emailMessage, scheduledAt, cancellationToken);
        }

        public async Task<List<EmailQueue>> GetPendingEmailsAsync(int batchSize = 10, CancellationToken cancellationToken = default)
        {
            var filterDescriptors = new List<FilterDescriptor>
            {
                new FilterDescriptor("Status", FilterOperator.Equal, "Pending"),
                new FilterDescriptor("ScheduledAt", FilterOperator.LessThanOrEqual, DateTime.Now)
            };

            var sortDescriptors = new List<SortDescriptor>
            {
                new SortDescriptor("ScheduledAt", SortOrder.Ascending)
            };

            return await GetList(null, 1, batchSize, sortDescriptors, filterDescriptors, cancellationToken);
        }

        public async Task UpdateEmailStatusAsync(EmailQueue emailQueue, CancellationToken cancellationToken = default)
        {
            emailQueue.UpdatedDate = DateTime.Now;
            await Upsert(emailQueue, emailQueue.Id, cancellationToken);
        }

        public async Task<int> RetryFailedEmailsAsync(CancellationToken cancellationToken = default)
        {
            var filterDescriptors = new List<FilterDescriptor>
            {
                new FilterDescriptor("Status", FilterOperator.Equal, "Failed"),
                new FilterDescriptor("RetryCount", FilterOperator.LessThan, 3),
                new FilterDescriptor("NextRetryAt", FilterOperator.LessThanOrEqual, DateTime.Now)
            };

            var failedEmails = await GetList(null, 1, 100, null, filterDescriptors, cancellationToken);

            foreach (var email in failedEmails)
            {
                email.Status = "Pending";
                email.NextRetryAt = null;
                await UpdateEmailStatusAsync(email, cancellationToken);
            }

            _logger.LogInformation("Retrying {Count} failed emails", failedEmails.Count);
            return failedEmails.Count;
        }

        #endregion

        #region Send Operations

        public async Task<bool> SendEmailAsync(EmailQueue emailQueue, CancellationToken cancellationToken = default)
        {
            try
            {
                if (emailQueue == null || string.IsNullOrEmpty(emailQueue.To))
                {
                    _logger.LogWarning("Email queue is null or has no recipients");
                    return false;
                }

                ValidateEmailSettings();

                // Update status to Sending
                emailQueue.Status = "Sending";
                await UpdateEmailStatusAsync(emailQueue, cancellationToken);

                using var smtpClient = CreateSmtpClient();
                using var mailMessage = CreateMailMessage(emailQueue);

                await smtpClient.SendMailAsync(mailMessage, cancellationToken);

                // Update status to Sent
                emailQueue.Status = "Sent";
                emailQueue.SentAt = DateTime.Now;
                emailQueue.ErrorMessage = null;
                await UpdateEmailStatusAsync(emailQueue, cancellationToken);

                _logger.LogInformation("Email sent successfully. Queue ID: {EmailId}", emailQueue.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email. Queue ID: {EmailId}", emailQueue.Id);

                // Update status to Failed
                emailQueue.Status = "Failed";
                emailQueue.RetryCount++;
                emailQueue.ErrorMessage = ex.Message;

                // Calculate next retry time using exponential backoff
                if (emailQueue.RetryCount < emailQueue.MaxRetries)
                {
                    var retryDelayMinutes = Math.Pow(2, emailQueue.RetryCount) * 5; // 5, 10, 20 minutes
                    emailQueue.NextRetryAt = DateTime.Now.AddMinutes(retryDelayMinutes);
                }

                await UpdateEmailStatusAsync(emailQueue, cancellationToken);
                return false;
            }
        }

        #endregion

        #region Private Helper Methods

        private SmtpClient CreateSmtpClient()
        {
            var smtpClient = new SmtpClient(_emailSettings.SmtpServer)
            {
                Port = _emailSettings.SmtpPort,
                EnableSsl = _emailSettings.EnableSsl,
                Timeout = _emailSettings.Timeout,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword)
            };

            return smtpClient;
        }

        private MailMessage CreateMailMessage(EmailQueue emailQueue)
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailSettings.FromEmail!, _emailSettings.FromName),
                Subject = emailQueue.Subject,
                Body = emailQueue.Body,
                IsBodyHtml = emailQueue.IsHtml
            };

            // Add recipients
            var toAddresses = JsonSerializer.Deserialize<List<EmailAddress>>(emailQueue.To);
            if (toAddresses != null)
            {
                foreach (var to in toAddresses)
                {
                    mailMessage.To.Add(new MailAddress(to.Email, to.DisplayName ?? string.Empty));
                }
            }

            // Add CC recipients
            if (!string.IsNullOrEmpty(emailQueue.Cc))
            {
                var ccAddresses = JsonSerializer.Deserialize<List<EmailAddress>>(emailQueue.Cc);
                if (ccAddresses != null)
                {
                    foreach (var cc in ccAddresses)
                    {
                        mailMessage.CC.Add(new MailAddress(cc.Email, cc.DisplayName ?? string.Empty));
                    }
                }
            }

            // Add BCC recipients
            if (!string.IsNullOrEmpty(emailQueue.Bcc))
            {
                var bccAddresses = JsonSerializer.Deserialize<List<EmailAddress>>(emailQueue.Bcc);
                if (bccAddresses != null)
                {
                    foreach (var bcc in bccAddresses)
                    {
                        mailMessage.Bcc.Add(new MailAddress(bcc.Email, bcc.DisplayName ?? string.Empty));
                    }
                }
            }

            // Add ReplyTo
            if (!string.IsNullOrEmpty(emailQueue.ReplyTo))
            {
                var replyTo = JsonSerializer.Deserialize<EmailAddress>(emailQueue.ReplyTo);
                if (replyTo != null)
                {
                    mailMessage.ReplyToList.Add(new MailAddress(replyTo.Email, replyTo.DisplayName ?? string.Empty));
                }
            }

            // Add attachments
            if (!string.IsNullOrEmpty(emailQueue.Attachments))
            {
                var attachments = JsonSerializer.Deserialize<List<EmailAttachment>>(emailQueue.Attachments);
                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        var stream = new MemoryStream(attachment.Content);
                        var mailAttachment = new Attachment(stream, attachment.FileName, attachment.ContentType);
                        mailMessage.Attachments.Add(mailAttachment);
                    }
                }
            }

            return mailMessage;
        }

        private void ValidateEmailSettings()
        {
            if (string.IsNullOrEmpty(_emailSettings.SmtpServer))
                throw new InvalidOperationException("SMTP server is not configured");

            if (string.IsNullOrEmpty(_emailSettings.FromEmail))
                throw new InvalidOperationException("From email is not configured");

            if (string.IsNullOrEmpty(_emailSettings.SmtpUsername))
                throw new InvalidOperationException("SMTP username is not configured");

            if (string.IsNullOrEmpty(_emailSettings.SmtpPassword))
                throw new InvalidOperationException("SMTP password is not configured");
        }

        #endregion
    }
}
