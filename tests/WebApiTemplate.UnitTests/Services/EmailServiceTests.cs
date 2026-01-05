using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using ApplicationCore.Entities;
using ApplicationCore.Models;
using ApplicationCore.Entities.Base;
using Infrastructure.Configurations;
using Infrastructure.Services;
using WebApiTemplate.TestFixtures.Builders;
using WebApiTemplate.UnitTests.Base;

namespace WebApiTemplate.UnitTests.Services;

public class EmailServiceTests : ServiceTestBase
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IOptions<AppConfig>> _mockAppConfig;
    private readonly Mock<IOptions<EmailSettings>> _mockEmailSettings;
    private readonly Mock<ILogger<EmailService>> _mockLogger;
    private readonly EmailService _service;
    private readonly AppDbContext _context;

    public EmailServiceTests()
    {
        _mockCache = new Mock<IDistributedCache>();
        _mockAppConfig = new Mock<IOptions<AppConfig>>();
        _mockEmailSettings = new Mock<IOptions<EmailSettings>>();
        _mockLogger = new Mock<ILogger<EmailService>>();

        // Setup AppConfig
        _mockAppConfig.Setup(x => x.Value).Returns(new AppConfig
        {
            UseCache = false
        });

        // Setup EmailSettings
        _mockEmailSettings.Setup(x => x.Value).Returns(new EmailSettings
        {
            SmtpServer = "smtp.test.com",
            SmtpPort = 587,
            SmtpUsername = "test@test.com",
            SmtpPassword = "testpassword",
            FromEmail = "noreply@test.com",
            FromName = "Test System",
            EnableSsl = true,
            Timeout = 30000
        });

        _context = CreateInMemoryDbContext();
        _service = new EmailService(_context, _mockCache.Object, _mockAppConfig.Object, _mockEmailSettings.Object, _mockLogger.Object);
    }

    #region QueueEmailAsync Tests

    [Fact]
    public async Task QueueEmailAsync_WithValidEmailMessage_CreatesEmailQueue()
    {
        // Arrange
        var emailMessage = new EmailMessage
        {
            To = new List<EmailAddress> { new EmailAddress("recipient@example.com") },
            Subject = "Test Email",
            Body = "Test Body",
            IsHtml = true
        };

        // Act
        var result = await _service.QueueEmailAsync(emailMessage, null, GetCancellationToken());

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Subject.Should().Be("Test Email");
        result.Body.Should().Be("Test Body");
        result.IsHtml.Should().BeTrue();
        result.Status.Should().Be("Pending");
        result.ScheduledAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task QueueEmailAsync_SerializesToField_ContainsEmailAddress()
    {
        // Arrange
        var emailMessage = new EmailMessage
        {
            To = new List<EmailAddress>
            {
                new EmailAddress("recipient1@example.com", "Recipient One"),
                new EmailAddress("recipient2@example.com", "Recipient Two")
            },
            Subject = "Multi-recipient Test",
            Body = "Test Body",
            IsHtml = false
        };

        // Act
        var result = await _service.QueueEmailAsync(emailMessage, null, GetCancellationToken());

        // Assert
        result.To.Should().NotBeNullOrEmpty();
        var deserializedTo = JsonSerializer.Deserialize<List<EmailAddress>>(result.To);
        deserializedTo.Should().HaveCount(2);
        deserializedTo![0].Email.Should().Be("recipient1@example.com");
        deserializedTo[1].Email.Should().Be("recipient2@example.com");
    }

    [Fact]
    public async Task QueueEmailAsync_WithCcAndBcc_SerializesCorrectly()
    {
        // Arrange
        var emailMessage = new EmailMessage
        {
            To = new List<EmailAddress> { new EmailAddress("to@example.com") },
            Cc = new List<EmailAddress> { new EmailAddress("cc@example.com") },
            Bcc = new List<EmailAddress> { new EmailAddress("bcc@example.com") },
            Subject = "Test with CC/BCC",
            Body = "Test Body",
            IsHtml = true
        };

        // Act
        var result = await _service.QueueEmailAsync(emailMessage, null, GetCancellationToken());

        // Assert
        result.Cc.Should().NotBeNullOrEmpty();
        result.Bcc.Should().NotBeNullOrEmpty();

        var deserializedCc = JsonSerializer.Deserialize<List<EmailAddress>>(result.Cc!);
        deserializedCc.Should().HaveCount(1);
        deserializedCc![0].Email.Should().Be("cc@example.com");

        var deserializedBcc = JsonSerializer.Deserialize<List<EmailAddress>>(result.Bcc!);
        deserializedBcc.Should().HaveCount(1);
        deserializedBcc![0].Email.Should().Be("bcc@example.com");
    }

    [Fact]
    public async Task QueueEmailAsync_WithScheduledTime_SetsScheduledAtCorrectly()
    {
        // Arrange
        var scheduledTime = DateTime.Now.AddHours(2);
        var emailMessage = new EmailMessage
        {
            To = new List<EmailAddress> { new EmailAddress("recipient@example.com") },
            Subject = "Scheduled Email",
            Body = "Test Body"
        };

        // Act
        var result = await _service.QueueEmailAsync(emailMessage, scheduledTime, GetCancellationToken());

        // Assert
        result.ScheduledAt.Should().BeCloseTo(scheduledTime, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region QueueSimpleEmailAsync Tests

    [Fact]
    public async Task QueueSimpleEmailAsync_WithValidData_CreatesEmailQueue()
    {
        // Arrange
        var toEmail = "simple@example.com";
        var subject = "Simple Email";
        var body = "Simple body";

        // Act
        var result = await _service.QueueSimpleEmailAsync(toEmail, subject, body, true, null, GetCancellationToken());

        // Assert
        result.Should().NotBeNull();
        result.Subject.Should().Be(subject);
        result.Body.Should().Be(body);
        result.IsHtml.Should().BeTrue();
        result.Status.Should().Be("Pending");

        var deserializedTo = JsonSerializer.Deserialize<List<EmailAddress>>(result.To);
        deserializedTo.Should().HaveCount(1);
        deserializedTo![0].Email.Should().Be(toEmail);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueueSimpleEmailAsync_WithIsHtmlFlag_SetsCorrectly(bool isHtml)
    {
        // Arrange
        var toEmail = "test@example.com";
        var subject = "Test";
        var body = "Body";

        // Act
        var result = await _service.QueueSimpleEmailAsync(toEmail, subject, body, isHtml, null, GetCancellationToken());

        // Assert
        result.IsHtml.Should().Be(isHtml);
    }

    #endregion

    #region QueueBulkEmailAsync Tests

    [Fact]
    public async Task QueueBulkEmailAsync_WithMultipleRecipients_CreatesEmailQueueWithAllRecipients()
    {
        // Arrange
        var toEmails = new List<string>
        {
            "user1@example.com",
            "user2@example.com",
            "user3@example.com"
        };
        var subject = "Bulk Email";
        var body = "Bulk body";

        // Act
        var result = await _service.QueueBulkEmailAsync(toEmails, subject, body, true, null, GetCancellationToken());

        // Assert
        result.Should().NotBeNull();
        result.Subject.Should().Be(subject);

        var deserializedTo = JsonSerializer.Deserialize<List<EmailAddress>>(result.To);
        deserializedTo.Should().HaveCount(3);
        deserializedTo!.Select(x => x.Email).Should().Contain(toEmails);
    }

    [Fact]
    public async Task QueueBulkEmailAsync_WithSingleRecipient_Works()
    {
        // Arrange
        var toEmails = new List<string> { "single@example.com" };

        // Act
        var result = await _service.QueueBulkEmailAsync(toEmails, "Subject", "Body");

        // Assert
        var deserializedTo = JsonSerializer.Deserialize<List<EmailAddress>>(result.To);
        deserializedTo.Should().HaveCount(1);
    }

    #endregion

    #region GetPendingEmailsAsync Tests

    [Fact]
    public async Task GetPendingEmailsAsync_WithPendingEmails_ReturnsOnlyPending()
    {
        // Arrange
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(1).AsPending().WithScheduledAt(DateTime.Now.AddMinutes(-5)).Build());
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(2).AsSent().WithScheduledAt(DateTime.Now.AddMinutes(-5)).Build());
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(3).AsPending().WithScheduledAt(DateTime.Now.AddMinutes(-3)).Build());
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(4).AsFailed("Error").WithScheduledAt(DateTime.Now.AddMinutes(-1)).Build());
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPendingEmailsAsync(10, GetCancellationToken());

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(e => e.Status == "Pending");
        result.Select(e => e.Id).Should().Contain(new[] { 1, 3 });
    }

    [Fact]
    public async Task GetPendingEmailsAsync_OnlyReturnsEmailsScheduledInPast()
    {
        // Arrange
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(1).AsPending().WithScheduledAt(DateTime.Now.AddMinutes(-5)).Build());
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(2).AsPending().WithScheduledAt(DateTime.Now.AddHours(1)).Build()); // Future
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPendingEmailsAsync(10, GetCancellationToken());

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(1);
    }

    [Fact]
    public async Task GetPendingEmailsAsync_RespectsBatchSize()
    {
        // Arrange
        for (int i = 1; i <= 20; i++)
        {
            _context.EmailQueue.Add(new EmailQueueBuilder()
                .WithId(i)
                .AsPending()
                .WithScheduledAt(DateTime.Now.AddMinutes(-i))
                .Build());
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPendingEmailsAsync(5, GetCancellationToken());

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetPendingEmailsAsync_OrdersByScheduledAtAscending()
    {
        // Arrange
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(1).AsPending().WithScheduledAt(DateTime.Now.AddMinutes(-10)).Build());
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(2).AsPending().WithScheduledAt(DateTime.Now.AddMinutes(-5)).Build());
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(3).AsPending().WithScheduledAt(DateTime.Now.AddMinutes(-20)).Build());
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPendingEmailsAsync(10, GetCancellationToken());

        // Assert
        result.Should().HaveCount(3);
        result[0].Id.Should().Be(3); // Oldest first
        result[1].Id.Should().Be(1);
        result[2].Id.Should().Be(2);
    }

    #endregion

    #region UpdateEmailStatusAsync Tests

    [Fact]
    public async Task UpdateEmailStatusAsync_UpdatesEmailInDatabase()
    {
        // Arrange
        var email = new EmailQueueBuilder().AsPending().Build();
        _context.EmailQueue.Add(email);
        await _context.SaveChangesAsync();

        // Modify status
        email.Status = "Sent";
        email.SentAt = DateTime.Now;

        // Act
        await _service.UpdateEmailStatusAsync(email, GetCancellationToken());

        // Assert
        var updated = await _context.EmailQueue.FindAsync(email.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Sent");
        updated.SentAt.Should().NotBeNull();
        updated.UpdatedDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region RetryFailedEmailsAsync Tests

    [Fact]
    public async Task RetryFailedEmailsAsync_WithFailedEmailsBelowMaxRetries_ResetsStatusToPending()
    {
        // Arrange
        var email1 = new EmailQueueBuilder()
            .WithId(1)
            .AsFailed("Error")
            .WithRetryCount(1)
            .WithMaxRetries(3)
            .WithNextRetryAt(DateTime.Now.AddMinutes(-1))
            .Build();
        var email2 = new EmailQueueBuilder()
            .WithId(2)
            .AsFailed("Error")
            .WithRetryCount(2)
            .WithMaxRetries(3)
            .WithNextRetryAt(DateTime.Now.AddMinutes(-1))
            .Build();

        _context.EmailQueue.AddRange(email1, email2);
        await _context.SaveChangesAsync();

        // Act
        var count = await _service.RetryFailedEmailsAsync(GetCancellationToken());

        // Assert
        count.Should().Be(2);

        var updated1 = await _context.EmailQueue.FindAsync(1);
        updated1!.Status.Should().Be("Pending");
        updated1.NextRetryAt.Should().BeNull();

        var updated2 = await _context.EmailQueue.FindAsync(2);
        updated2!.Status.Should().Be("Pending");
        updated2.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public async Task RetryFailedEmailsAsync_WithFailedEmailsAtMaxRetries_DoesNotRetry()
    {
        // Arrange
        var email = new EmailQueueBuilder()
            .AsFailed("Error")
            .WithRetryCount(3)
            .WithMaxRetries(3)
            .WithNextRetryAt(DateTime.Now.AddMinutes(-1))
            .Build();

        _context.EmailQueue.Add(email);
        await _context.SaveChangesAsync();

        // Act
        var count = await _service.RetryFailedEmailsAsync(GetCancellationToken());

        // Assert
        count.Should().Be(0);

        var notUpdated = await _context.EmailQueue.FindAsync(email.Id);
        notUpdated!.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task RetryFailedEmailsAsync_OnlyRetriesEmailsWithNextRetryAtInPast()
    {
        // Arrange
        var email1 = new EmailQueueBuilder()
            .WithId(1)
            .AsFailed("Error")
            .WithRetryCount(1)
            .WithNextRetryAt(DateTime.Now.AddMinutes(-1)) // Past - should retry
            .Build();
        var email2 = new EmailQueueBuilder()
            .WithId(2)
            .AsFailed("Error")
            .WithRetryCount(1)
            .WithNextRetryAt(DateTime.Now.AddHours(1)) // Future - should not retry
            .Build();

        _context.EmailQueue.AddRange(email1, email2);
        await _context.SaveChangesAsync();

        // Act
        var count = await _service.RetryFailedEmailsAsync(GetCancellationToken());

        // Assert
        count.Should().Be(1);

        var updated = await _context.EmailQueue.FindAsync(1);
        updated!.Status.Should().Be("Pending");

        var notUpdated = await _context.EmailQueue.FindAsync(2);
        notUpdated!.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task RetryFailedEmailsAsync_WithNoFailedEmails_ReturnsZero()
    {
        // Arrange
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(1).AsPending().Build());
        _context.EmailQueue.Add(new EmailQueueBuilder().WithId(2).AsSent().Build());
        await _context.SaveChangesAsync();

        // Act
        var count = await _service.RetryFailedEmailsAsync(GetCancellationToken());

        // Assert
        count.Should().Be(0);
    }

    #endregion

    public override void Dispose()
    {
        _context?.Dispose();
        base.Dispose();
    }
}
