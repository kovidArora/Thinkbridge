using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OrderSystem.Api.ErrorHandling;

/// <summary>
/// Central catch-all for genuinely unexpected exceptions (bugs, infra
/// faults). Replaces the controller's blanket
/// `catch (Exception) { return "An unexpected error occurred"; }`, which
/// hid real bugs (like the off-by-one crash) behind a 200-with-success-false
/// response. This handler logs the full exception and returns a proper
/// 500 ProblemDetails response — it does not swallow anything.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception processing {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred."
        }, cancellationToken);

        return true;
    }
}
