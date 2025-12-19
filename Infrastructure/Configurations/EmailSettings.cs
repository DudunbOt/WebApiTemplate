namespace Infrastructure.Configurations
{
    public class EmailSettings
    {
        public string? SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string? SmtpUsername { get; set; }
        public string? SmtpPassword { get; set; }
        public string? FromEmail { get; set; }
        public string? FromName { get; set; }
        public bool EnableSsl { get; set; }
        public int Timeout { get; set; } = 30000; // 30 seconds default
    }
}
