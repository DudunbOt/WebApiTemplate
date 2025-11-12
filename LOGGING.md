# Logging with NLog

This project uses **NLog** for comprehensive logging with file output and console display.

## Log Locations

Logs are written to the `logs/` directory:

- **`logs/nlog-all-YYYY-MM-DD.log`** - All logs including Microsoft framework logs
- **`logs/nlog-own-YYYY-MM-DD.log`** - Your application logs only (excludes Microsoft noise)
- **`logs/internal-nlog.txt`** - NLog internal diagnostics (troubleshooting NLog itself)

Logs are automatically rotated daily with the date in the filename.

## What's Logged

### In Every Log Entry

All log entries include:
- **Timestamp** - When the event occurred
- **Log Level** - Trace, Debug, Info, Warn, Error, Fatal
- **Logger Name** - Which class logged the message
- **Message** - The log message
- **Exception** - Full exception details if present
- **TraceId** - Unique identifier for each HTTP request (`aspnet-TraceIdentifier`)

### Example Log Entry

```
2025-11-12 07:30:15.1234|0|ERROR|WebApi.Middleware.GlobalExceptionHandlerMiddleware|An unhandled exception occurred: UserInfo with key '5' was not found.|url: https://localhost:5001/api/userinfo/5|action: GetUser|traceId: 0HMVVQK3L5V8J:00000001
```

## TraceId Correlation

The **TraceId** is automatically included in:
1. **Log files** - See `traceId:` in the log layout
2. **Error responses** - Returned to the client in the JSON error response
3. **Console output** - Visible when running the app

This allows you to:
- User reports error with TraceId `0HMVVQK3L5V8J`
- Search logs: `grep "0HMVVQK3L5V8J" logs/nlog-own-*.log`
- Find all related log entries for that specific request

## Using the Logger

### In Controllers/Services

The logger is automatically injected via dependency injection:

```csharp
public class YourController : ControllerBase
{
    private readonly ILogger<YourController> _logger;

    public YourController(ILogger<YourController> logger)
    {
        _logger = logger;
    }

    public IActionResult SomeAction()
    {
        _logger.LogInformation("Processing action");
        _logger.LogWarning("Something unexpected happened");
        _logger.LogError(exception, "An error occurred");

        return Ok();
    }
}
```

### Log Levels

- **Trace** - Very detailed, typically only for debugging
- **Debug** - Debugging information
- **Info** - General informational messages
- **Warn** - Warning messages for non-critical issues
- **Error** - Error messages for failures
- **Fatal** - Critical errors causing application shutdown

### Example Usage

```csharp
// Simple message
_logger.LogInformation("User {UserId} logged in", userId);

// With exception
try
{
    // code
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process user {UserId}", userId);
    throw;
}

// Structured logging
_logger.LogInformation("Order {OrderId} created by {UserId} for {Amount:C}",
    order.Id, user.Id, order.Total);
```

## Log Configuration

Configuration is in [WebApi/nlog.config](WebApi/nlog.config).

### Key Settings

- **autoReload="true"** - Changes take effect without restarting
- **File rotation** - Daily rotation with `${shortdate}` in filename
- **Console output** - Shows logs in development with TraceId
- **Log levels** - Microsoft logs limited to Info and above (reduces noise)

### Customizing Log Levels

Edit `nlog.config` to change what gets logged:

```xml
<!-- Log everything from your code -->
<logger name="*" minlevel="Trace" writeTo="ownFile-web,console" />

<!-- Or only warnings and above -->
<logger name="*" minlevel="Warn" writeTo="ownFile-web,console" />
```

### Adding New Log Targets

Add to `<targets>` section in `nlog.config`:

```xml
<!-- Email on errors -->
<target xsi:type="Mail" name="emailTarget"
        subject="Error in WebApi"
        to="admin@example.com"
        from="noreply@example.com"
        smtpServer="smtp.example.com" />

<logger name="*" minlevel="Error" writeTo="emailTarget" />
```

Other targets: Database, EventLog, Slack, etc. See [NLog targets](https://nlog-project.org/config/?tab=targets).

## Production Considerations

### 1. Log Retention

Logs accumulate daily. Consider:
- Implement log file archiving/cleanup
- Use log aggregation services (e.g., Seq, ELK, Application Insights)
- Set up automated log rotation

### 2. Performance

Current config is optimized for development. For production:

```xml
<!-- Async wrapper for better performance -->
<targets async="true">
    <target xsi:type="File" name="allfile" ... />
</targets>
```

### 3. Sensitive Data

Never log:
- Passwords
- API keys
- Personal identifiable information (PII)
- Credit card numbers

The current setup already filters passwords from `UserInfo` via AutoMapper.

### 4. Log Level in Production

In production, consider raising the minimum level:

```xml
<logger name="*" minlevel="Info" writeTo="ownFile-web" />
```

## Troubleshooting

### Logs not appearing

1. Check `logs/internal-nlog.txt` for NLog errors
2. Ensure `nlog.config` is copied to output directory (already configured in WebApi.csproj)
3. Verify the `logs/` directory is writable

### Too many logs

Reduce noise by adjusting log levels in `nlog.config`:

```xml
<!-- Increase minimum level -->
<logger name="*" minlevel="Warn" writeTo="ownFile-web" />
```

### TraceId not showing

TraceId is automatically included via `${aspnet-TraceIdentifier}` in the layout. If missing:
1. Ensure you're using `ILogger<T>` from dependency injection
2. Check that NLog.Web.AspNetCore is properly installed

## Integration with Global Exception Handler

The [GlobalExceptionHandlerMiddleware](WebApi/Middleware/GlobalExceptionHandlerMiddleware.cs#L38) automatically logs all unhandled exceptions:

```csharp
_logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);
```

This log entry will include:
- Full exception details
- Stack trace
- HTTP request details (URL, action)
- **TraceId** - matching the TraceId in the error response sent to the client

## Example: Finding Errors by TraceId

**Scenario:** User reports error with TraceId `0HMVVQK3L5V8J`

**Find all logs for that request:**
```bash
# Windows PowerShell
Select-String -Path "logs\*.log" -Pattern "0HMVVQK3L5V8J"

# Linux/Mac
grep "0HMVVQK3L5V8J" logs/*.log

# Or just the error
grep "ERROR.*0HMVVQK3L5V8J" logs/nlog-own-*.log
```

**Result:**
```
2025-11-12 07:30:15|INFO|Starting request GET /api/userinfo/5|traceId: 0HMVVQK3L5V8J
2025-11-12 07:30:15|ERROR|An unhandled exception occurred|traceId: 0HMVVQK3L5V8J
2025-11-12 07:30:15|INFO|Request completed 404|traceId: 0HMVVQK3L5V8J
```

## References

- [NLog Documentation](https://nlog-project.org/)
- [NLog Targets](https://nlog-project.org/config/?tab=targets)
- [ASP.NET Core Integration](https://github.com/NLog/NLog/wiki/Getting-started-with-ASP.NET-Core-6)
