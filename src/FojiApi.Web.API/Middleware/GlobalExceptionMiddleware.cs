using System.Net;
using System.Text.Json;
using FojiApi.Core.Exceptions;

namespace FojiApi.Web.API.Middleware;

public class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment env,
    IConfiguration configuration)
{
    /// <summary>
    /// Deployed environments run as Production, so IsDevelopment() is false and a
    /// 500 comes back with no detail at all — leaving nothing to debug from
    /// without CloudWatch. Debug:ExposeErrorDetail opts a non-production
    /// environment into returning the exception. Defaults to false; never enable
    /// it on a tenant-facing deployment, as stack traces leak internals.
    /// </summary>
    private bool ExposeErrorDetail =>
        env.IsDevelopment() || configuration.GetValue<bool?>("Debug:ExposeErrorDetail") == true;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var traceId = context.TraceIdentifier;
        var (statusCode, message) = ex switch
        {
            DomainException e       => (HttpStatusCode.BadRequest,           e.Message),
            ConflictException e     => (HttpStatusCode.Conflict,             e.Message),
            NotFoundException e     => (HttpStatusCode.NotFound,             e.Message),
            ForbiddenException e    => (HttpStatusCode.Forbidden,            e.Message),
            UnauthorizedAccessException e => (HttpStatusCode.Unauthorized,   e.Message),
            _                       => (HttpStatusCode.InternalServerError,  "An unexpected error occurred.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", traceId);
        else
            logger.LogWarning("Handled exception [{Type}] {Message}. TraceId: {TraceId}", ex.GetType().Name, ex.Message, traceId);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse(
            Error: message,
            TraceId: traceId,
            Detail: ExposeErrorDetail && statusCode == HttpStatusCode.InternalServerError ? ex.ToString() : null
        );

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}

public record ErrorResponse(string Error, string TraceId, string? Detail);
