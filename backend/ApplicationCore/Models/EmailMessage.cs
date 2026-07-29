namespace ApplicationCore.Models
{
    public class EmailMessage
    {
        public List<EmailAddress> To { get; set; } = new();
        public List<EmailAddress>? Cc { get; set; }
        public List<EmailAddress>? Bcc { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
        public List<EmailAttachment>? Attachments { get; set; }
        public EmailAddress? ReplyTo { get; set; }
    }

    public class EmailAddress
    {
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }

        public EmailAddress() { }

        public EmailAddress(string email, string? displayName = null)
        {
            Email = email;
            DisplayName = displayName;
        }
    }

    public class EmailAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";

        public EmailAttachment() { }

        public EmailAttachment(string fileName, byte[] content, string contentType = "application/octet-stream")
        {
            FileName = fileName;
            Content = content;
            ContentType = contentType;
        }
    }
}
