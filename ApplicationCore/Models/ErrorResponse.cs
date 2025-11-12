namespace ApplicationCore.Models;

public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; }
    public string? Details { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
    public string? TraceId { get; set; }
    public DateTime Timestamp { get; set; }

    public ErrorResponse(int statusCode, string message)
    {
        StatusCode = statusCode;
        Message = message;
        Timestamp = DateTime.UtcNow;
    }

    public ErrorResponse(int statusCode, string message, string? details = null)
    {
        StatusCode = statusCode;
        Message = message;
        Details = details;
        Timestamp = DateTime.UtcNow;
    }
}
