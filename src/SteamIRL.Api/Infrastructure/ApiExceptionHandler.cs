using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace SteamIRL.Api.Infrastructure;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var unavailable = exception is NpgsqlException or TimeoutException
            or InvalidOperationException { InnerException: NpgsqlException };
        context.Response.StatusCode = exception is BadHttpRequestException badRequest ? badRequest.StatusCode
            : unavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
        // Do not log exception messages: database errors can contain submitted values.
        if (context.Response.StatusCode >= 500)
            logger.LogError("Request {TraceId} failed with {ExceptionType}", context.TraceIdentifier, exception.GetType().Name);
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = context.Response.StatusCode < 500 ? "Некорректные данные запроса."
                    : unavailable ? "Service temporarily unavailable" : "An unexpected error occurred",
                Extensions = { ["traceId"] = context.TraceIdentifier }
            }
        });
    }
}
