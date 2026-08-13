using Microsoft.AspNetCore.Authorization;
using QuotesApi.Repositories;
using System.Security.Claims;

namespace QuotesApi.Authorization;

public class MustOwnQuoteHandler : AuthorizationHandler<MustOwnQuoteRequirement>
{
    private readonly IQuoteRepository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MustOwnQuoteHandler(
        IQuoteRepository repository,
        IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MustOwnQuoteRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return;
        }

        var routeIdValue = httpContext.Request.RouteValues["id"]?.ToString();

        if (!int.TryParse(routeIdValue, out var quoteId))
        {
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return;
        }

        var quote = await _repository.GetByIdAsync(quoteId, httpContext.RequestAborted);

        if (quote is not null && quote.CreatedByUserId == userId)
        {
            context.Succeed(requirement);
        }
    }
}