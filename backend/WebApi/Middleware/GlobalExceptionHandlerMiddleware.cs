using ApplicationCore.Exceptions;
using ApplicationCore.Models;
using System.Net;
using System.Text.Json;

namespace WebApi.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        context.Response.ContentType = "application/json";

        var errorResponse = exception switch
        {
            NotFoundException notFoundEx => new ErrorResponse(
                (int)HttpStatusCode.NotFound,
                "Resource not found",
                notFoundEx.Message)
            {
                TraceId = context.TraceIdentifier
            },

            ValidationException validationEx => new ErrorResponse(
                (int)HttpStatusCode.BadRequest,
                "Validation failed",
                validationEx.Message)
            {
                Errors = validationEx.Errors,
                TraceId = context.TraceIdentifier
            },

            UnauthorizedException unauthorizedEx => new ErrorResponse(
                (int)HttpStatusCode.Unauthorized,
                "Unauthorized",
                unauthorizedEx.Message)
            {
                TraceId = context.TraceIdentifier
            },

            ForbiddenException forbiddenEx => new ErrorResponse(
                (int)HttpStatusCode.Forbidden,
                "Forbidden",
                forbiddenEx.Message)
            {
                TraceId = context.TraceIdentifier
            },

            BadRequestException badRequestEx => new ErrorResponse(
                (int)HttpStatusCode.BadRequest,
                "Bad request",
                badRequestEx.Message)
            {
                TraceId = context.TraceIdentifier
            },

            ConflictException conflictEx => new ErrorResponse(
                (int)HttpStatusCode.Conflict,
                "Conflict",
                conflictEx.Message)
            {
                TraceId = context.TraceIdentifier
            },

            ArgumentNullException argNullEx => new ErrorResponse(
                (int)HttpStatusCode.BadRequest,
                "Invalid argument",
                $"The parameter '{argNullEx.ParamName}' cannot be null.")
            {
                TraceId = context.TraceIdentifier
            },

            ArgumentException argEx => new ErrorResponse(
                (int)HttpStatusCode.BadRequest,
                "Invalid argument",
                argEx.Message)
            {
                TraceId = context.TraceIdentifier
            },

            InvalidOperationException invalidOpEx => new ErrorResponse(
                (int)HttpStatusCode.BadRequest,
                "Invalid operation",
                invalidOpEx.Message)
            {
                TraceId = context.TraceIdentifier
            },

            _ => new ErrorResponse(
                (int)HttpStatusCode.InternalServerError,
                "An error occurred while processing your request",
                _env.IsDevelopment() ? exception.Message : null)
            {
                Details = _env.IsDevelopment() ? exception.StackTrace : null,
                TraceId = context.TraceIdentifier
            }
        };

        context.Response.StatusCode = errorResponse.StatusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _env.IsDevelopment()
        };

        var json = JsonSerializer.Serialize(errorResponse, options);
        await context.Response.WriteAsync(json);
    }
}
