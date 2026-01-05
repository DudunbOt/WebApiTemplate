using ApplicationCore.Entities;

namespace WebApiTemplate.TestFixtures.Builders;

/// <summary>
/// Test data builder for EmailQueue entities following the Builder pattern
/// </summary>
public class EmailQueueBuilder
{
    private int _id = 1;
    private string _to = "recipient@example.com";
    private string? _cc;
    private string? _bcc;
    private string _subject = "Test Email";
    private string _body = "Test email body";
    private bool _isHtml = false;
    private string _status = "Pending";
    private int _retryCount = 0;
    private int _maxRetries = 3;
    private string? _errorMessage;
    private DateTime? _scheduledAt;
    private DateTime? _sentAt;
    private DateTime? _nextRetryAt;
    private DateTime _createdDate = DateTime.UtcNow;
    private DateTime? _updatedDate;
    private DateTime? _deletedDate;

    public EmailQueueBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public EmailQueueBuilder WithTo(string to)
    {
        _to = to;
        return this;
    }

    public EmailQueueBuilder WithCc(string cc)
    {
        _cc = cc;
        return this;
    }

    public EmailQueueBuilder WithBcc(string bcc)
    {
        _bcc = bcc;
        return this;
    }

    public EmailQueueBuilder WithSubject(string subject)
    {
        _subject = subject;
        return this;
    }

    public EmailQueueBuilder WithBody(string body)
    {
        _body = body;
        return this;
    }

    public EmailQueueBuilder WithHtmlBody(string htmlBody)
    {
        _body = htmlBody;
        _isHtml = true;
        return this;
    }

    public EmailQueueBuilder WithStatus(string status)
    {
        _status = status;
        return this;
    }

    public EmailQueueBuilder AsPending()
    {
        _status = "Pending";
        return this;
    }

    public EmailQueueBuilder AsSending()
    {
        _status = "Sending";
        return this;
    }

    public EmailQueueBuilder AsSent(DateTime? sentAt = null)
    {
        _status = "Sent";
        _sentAt = sentAt ?? DateTime.UtcNow;
        return this;
    }

    public EmailQueueBuilder AsFailed(string errorMessage)
    {
        _status = "Failed";
        _errorMessage = errorMessage;
        return this;
    }

    public EmailQueueBuilder WithRetryCount(int retryCount)
    {
        _retryCount = retryCount;
        return this;
    }

    public EmailQueueBuilder WithMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    public EmailQueueBuilder WithScheduledAt(DateTime scheduledAt)
    {
        _scheduledAt = scheduledAt;
        return this;
    }

    public EmailQueueBuilder WithNextRetryAt(DateTime nextRetryAt)
    {
        _nextRetryAt = nextRetryAt;
        return this;
    }

    public EmailQueueBuilder WithErrorMessage(string errorMessage)
    {
        _errorMessage = errorMessage;
        return this;
    }

    public EmailQueueBuilder WithCreatedDate(DateTime createdDate)
    {
        _createdDate = createdDate;
        return this;
    }

    public EmailQueueBuilder AsDeleted(DateTime? deletedDate = null)
    {
        _deletedDate = deletedDate ?? DateTime.UtcNow;
        return this;
    }

    public EmailQueue Build()
    {
        return new EmailQueue
        {
            Id = _id,
            To = _to,
            Cc = _cc,
            Bcc = _bcc,
            Subject = _subject,
            Body = _body,
            IsHtml = _isHtml,
            Status = _status,
            RetryCount = _retryCount,
            MaxRetries = _maxRetries,
            ErrorMessage = _errorMessage,
            ScheduledAt = _scheduledAt,
            SentAt = _sentAt,
            NextRetryAt = _nextRetryAt,
            CreatedDate = _createdDate,
            UpdatedDate = _updatedDate,
            DeletedDate = _deletedDate
        };
    }

    /// <summary>
    /// Creates a default EmailQueue for testing
    /// </summary>
    public static EmailQueue Default() => new EmailQueueBuilder().Build();

    /// <summary>
    /// Creates multiple EmailQueue entities for testing
    /// </summary>
    public static List<EmailQueue> CreateMany(int count, string status = "Pending")
    {
        var emails = new List<EmailQueue>();
        for (int i = 1; i <= count; i++)
        {
            emails.Add(new EmailQueueBuilder()
                .WithId(i)
                .WithTo($"recipient{i}@example.com")
                .WithSubject($"Test Email {i}")
                .WithStatus(status)
                .Build());
        }
        return emails;
    }

    /// <summary>
    /// Creates an email ready for retry
    /// </summary>
    public static EmailQueue ReadyForRetry()
    {
        return new EmailQueueBuilder()
            .AsFailed("SMTP connection failed")
            .WithRetryCount(1)
            .WithMaxRetries(3)
            .WithNextRetryAt(DateTime.UtcNow.AddMinutes(-1))
            .Build();
    }
}
