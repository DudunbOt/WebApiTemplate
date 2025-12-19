using ApplicationCore.Entities;
using ApplicationCore.Models;

namespace ApplicationCore.Interfaces
{
    public partial interface IEmailService : IServiceBase<EmailQueue>
    {
        /// <summary>
        /// Queues an email message to be sent asynchronously
        /// </summary>
        Task<EmailQueue> QueueEmailAsync(EmailMessage emailMessage, DateTime? scheduledAt = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queues a simple email without complex configuration
        /// </summary>
        Task<EmailQueue> QueueSimpleEmailAsync(string toEmail, string subject, string body, bool isHtml = true, DateTime? scheduledAt = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queues email to multiple recipients
        /// </summary>
        Task<EmailQueue> QueueBulkEmailAsync(List<string> toEmails, string subject, string body, bool isHtml = true, DateTime? scheduledAt = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Internal method: Actually sends an email (called by background service)
        /// </summary>
        Task<bool> SendEmailAsync(EmailQueue emailQueue, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets pending emails from queue that are ready to be sent
        /// </summary>
        Task<List<EmailQueue>> GetPendingEmailsAsync(int batchSize = 10, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates email queue status
        /// </summary>
        Task UpdateEmailStatusAsync(EmailQueue emailQueue, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retries failed emails
        /// </summary>
        Task<int> RetryFailedEmailsAsync(CancellationToken cancellationToken = default);
    }
}
