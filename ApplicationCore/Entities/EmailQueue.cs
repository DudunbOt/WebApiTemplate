using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApplicationCore.Entities
{
    public partial class EmailQueue : EntityBase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(2000)]
        public string To { get; set; } = string.Empty; // JSON array of email addresses

        [MaxLength(2000)]
        public string? Cc { get; set; } // JSON array of email addresses

        [MaxLength(2000)]
        public string? Bcc { get; set; } // JSON array of email addresses

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public bool IsHtml { get; set; } = true;

        public string? Attachments { get; set; } // JSON array of attachments

        [MaxLength(200)]
        public string? ReplyTo { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Sending, Sent, Failed

        public int RetryCount { get; set; } = 0;

        public int MaxRetries { get; set; } = 3;

        public string? ErrorMessage { get; set; }

        public DateTime? ScheduledAt { get; set; }

        public DateTime? SentAt { get; set; }

        public DateTime? NextRetryAt { get; set; }
    }
}
