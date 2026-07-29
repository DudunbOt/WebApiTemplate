# Hangfire Background Jobs

This project uses [Hangfire](https://www.hangfire.io/) for background job processing. Hangfire provides reliable background job processing with persistence, automatic retries, and a built-in dashboard.

## Table of Contents

- [Overview](#overview)
- [Configuration](#configuration)
- [Dashboard Access](#dashboard-access)
- [Creating Background Jobs](#creating-background-jobs)
- [Job Types](#job-types)
- [Monitoring & Troubleshooting](#monitoring--troubleshooting)
- [Production Considerations](#production-considerations)

## Overview

Hangfire is configured to:
- Store jobs in the same database as the application (SQL Server or PostgreSQL)
- Automatically retry failed jobs with exponential backoff
- Provide a web dashboard for monitoring at `/hangfire`
- Auto-register recurring jobs via attributes

## Configuration

### appsettings.json

```json
{
  "Hangfire": {
    "WorkerCount": 5,
    "Queues": ["default", "emails"],
    "Dashboard": {
      "Username": "admin",
      "Password": "changeme"
    }
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `WorkerCount` | Number of concurrent worker threads | 5 |
| `Queues` | List of queues to process | ["default"] |
| `Dashboard.Username` | Dashboard login username | - |
| `Dashboard.Password` | Dashboard login password | - |

### Database Storage

Hangfire automatically uses the same database provider configured for the application:

- **PostgreSQL**: Creates tables with `hangfire.` schema prefix
- **SQL Server**: Creates tables with `HangFire.` schema prefix

Tables are created automatically on first startup.

## Dashboard Access

The Hangfire Dashboard is available at:

```
https://your-app-url/hangfire
```

### Authentication

The dashboard requires Basic Authentication. When you access `/hangfire`, your browser will prompt for credentials.

**Development**: Use credentials from `appsettings.json`

**Production**: Override credentials using environment variables:

```bash
# Linux/macOS
export Hangfire__Dashboard__Username=admin
export Hangfire__Dashboard__Password=your-secure-password

# Windows PowerShell
$env:Hangfire__Dashboard__Username = "admin"
$env:Hangfire__Dashboard__Password = "your-secure-password"

# Windows Command Prompt
set Hangfire__Dashboard__Username=admin
set Hangfire__Dashboard__Password=your-secure-password
```

Or use a secrets manager (Azure Key Vault, AWS Secrets Manager, etc.).

## Creating Background Jobs

### Step 1: Create a Job Class

Create a new class in `Infrastructure/Jobs/` that implements `IHangfireJob`:

```csharp
using ApplicationCore.Attributes;
using ApplicationCore.Interfaces.Base;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs
{
    [RecurringJob("my-job-id", "0 * * * *", Queue = "default")]
    public class MyJob : IHangfireJob
    {
        private readonly ILogger<MyJob> _logger;
        private readonly IMyService _myService;

        public MyJob(ILogger<MyJob> logger, IMyService myService)
        {
            _logger = logger;
            _myService = myService;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("MyJob started");

            // Your job logic here
            await _myService.DoWorkAsync();

            _logger.LogInformation("MyJob completed");
        }
    }
}
```

### Step 2: That's It!

The job is automatically:
- Registered with dependency injection (via `IHangfireJob` interface)
- Scheduled with Hangfire (via `[RecurringJob]` attribute)

No changes to `Program.cs` required.

### RecurringJob Attribute

```csharp
[RecurringJob("job-id", "cron-expression", Queue = "queue-name", TimeZone = "UTC")]
```

| Parameter | Description | Required |
|-----------|-------------|----------|
| `jobId` | Unique identifier for the job | Yes |
| `cronExpression` | Cron schedule expression | Yes |
| `Queue` | Queue name for the job | No (default: "default") |
| `TimeZone` | Timezone for the schedule | No (default: "UTC") |

### Common Cron Expressions

| Expression | Description |
|------------|-------------|
| `* * * * *` | Every minute |
| `*/5 * * * *` | Every 5 minutes |
| `0 * * * *` | Every hour |
| `0 0 * * *` | Daily at midnight |
| `0 0 * * 0` | Weekly on Sunday at midnight |
| `0 0 1 * *` | Monthly on the 1st at midnight |

Use [crontab.guru](https://crontab.guru/) to build and verify cron expressions.

## Job Types

### Recurring Jobs (Scheduled)

Jobs that run on a schedule. Use the `[RecurringJob]` attribute.

```csharp
[RecurringJob("cleanup-job", "0 3 * * *")] // Daily at 3 AM
public class CleanupJob : IHangfireJob
{
    public async Task ExecuteAsync() { /* ... */ }
}
```

### Fire-and-Forget Jobs

Jobs that run once immediately. Enqueue from anywhere in your code:

```csharp
using Hangfire;

// Enqueue a job to run immediately
BackgroundJob.Enqueue<IEmailService>(x => x.SendWelcomeEmailAsync(userId));

// Or with a specific job class
BackgroundJob.Enqueue<MyJob>(x => x.ExecuteAsync());
```

### Delayed Jobs

Jobs that run once after a delay:

```csharp
// Run in 24 hours
BackgroundJob.Schedule<IEmailService>(
    x => x.SendReminderAsync(userId),
    TimeSpan.FromHours(24));

// Run at a specific time
BackgroundJob.Schedule<IEmailService>(
    x => x.SendReminderAsync(userId),
    new DateTime(2024, 12, 31, 23, 59, 0));
```

### Continuation Jobs

Jobs that run after another job completes:

```csharp
var jobId = BackgroundJob.Enqueue<IOrderService>(x => x.ProcessOrderAsync(orderId));

BackgroundJob.ContinueWith<IEmailService>(
    jobId,
    x => x.SendOrderConfirmationAsync(orderId));
```

## Monitoring & Troubleshooting

### Dashboard Features

The Hangfire Dashboard (`/hangfire`) provides:

- **Jobs**: View all jobs (succeeded, failed, processing, scheduled, enqueued)
- **Retries**: See jobs waiting to be retried
- **Recurring Jobs**: View and manage scheduled recurring jobs
- **Servers**: See active Hangfire servers/workers
- **Queues**: Monitor queue depths

### Common Issues

#### Jobs Not Running

1. Check if Hangfire server is running (Dashboard > Servers)
2. Verify the queue name matches between job and configuration
3. Check for exceptions in the job (Dashboard > Failed)

#### Jobs Failing

1. Check the exception details in Dashboard > Failed
2. Verify all dependencies are registered in DI
3. Check database connectivity
4. Review logs for detailed error messages

#### Dashboard Not Loading

1. Verify credentials are correct
2. Check if Basic Auth header is being sent
3. Ensure Hangfire tables exist in database

### Logging

Jobs use the standard `ILogger<T>` interface. All job logs appear in your configured logging output (NLog).

```csharp
_logger.LogInformation("Processing item {ItemId}", itemId);
_logger.LogError(ex, "Failed to process item {ItemId}", itemId);
```

## Production Considerations

### Security

1. **Use strong passwords**: Generate a secure password for dashboard access
2. **Use environment variables**: Never commit production credentials
3. **Consider IP restrictions**: Use reverse proxy rules to limit dashboard access

### Performance

1. **Worker count**: Adjust based on CPU cores and job types
   - CPU-bound jobs: Workers = CPU cores
   - I/O-bound jobs: Workers = CPU cores * 2-4

2. **Queue separation**: Use separate queues for different job priorities
   ```csharp
   [RecurringJob("critical-job", "* * * * *", Queue = "critical")]
   [RecurringJob("low-priority", "0 * * * *", Queue = "low")]
   ```

3. **Database maintenance**: Periodically clean up old job data
   ```sql
   -- PostgreSQL: Delete succeeded jobs older than 7 days
   DELETE FROM hangfire.job
   WHERE statename = 'Succeeded'
   AND createdat < NOW() - INTERVAL '7 days';
   ```

### High Availability

For multiple server instances:

1. Each instance runs its own Hangfire server
2. Jobs are distributed across servers automatically
3. If a server fails, another picks up its jobs
4. Use `DisableGlobalLocks = true` for SQL Server (already configured)

### Monitoring

Consider integrating with:
- Application Insights
- Prometheus + Grafana
- Custom health checks

Example health check (already included):
```
GET /health/ready  # Checks database connectivity
```

## Existing Jobs

### EmailQueueJob

**ID**: `process-email-queue`
**Schedule**: Every 5 minutes (`*/5 * * * *`)
**Queue**: `emails`

Processes the email queue:
1. Retries previously failed emails (up to 3 attempts)
2. Sends pending emails (batch of 10)
3. Updates email status in database

```csharp
[RecurringJob("process-email-queue", "*/5 * * * *", Queue = "emails")]
public class EmailQueueJob : IHangfireJob
```

## Quick Reference

### Add a New Recurring Job

```csharp
// Infrastructure/Jobs/MyNewJob.cs
[RecurringJob("my-new-job", "0 */6 * * *", Queue = "default")]
public class MyNewJob : IHangfireJob
{
    public async Task ExecuteAsync()
    {
        // Job logic
    }
}
```

### Enqueue a One-Time Job

```csharp
BackgroundJob.Enqueue<MyService>(x => x.DoSomethingAsync());
```

### Schedule a Delayed Job

```csharp
BackgroundJob.Schedule<MyService>(
    x => x.DoSomethingAsync(),
    TimeSpan.FromHours(1));
```

### Trigger a Recurring Job Manually

```csharp
RecurringJob.TriggerJob("process-email-queue");
```

### Remove a Recurring Job

```csharp
RecurringJob.RemoveIfExists("old-job-id");
```
