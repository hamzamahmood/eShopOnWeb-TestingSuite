using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Applies a lifecycle action (pause / resume / cancel / reactivate) to the
/// authenticated caller's own subscription (UC4).
/// </summary>
public class LifecycleEndpoint : IEndpoint<IResult, LifecycleRequest, ClaimsPrincipal>
{
    private readonly ISubscriptionService _subscriptionService;

    public LifecycleEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/lifecycle",
                [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
                (LifecycleRequest request, ClaimsPrincipal user) => await HandleAsync(request, user))
            .Produces<CustomerSubscriptionDto>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(LifecycleRequest request, ClaimsPrincipal user)
    {
        var reference = user.Identity?.Name;
        if (string.IsNullOrEmpty(reference))
        {
            return Results.Unauthorized();
        }

        if (!SubscriptionMappings.TryParseAction(request.Action, out var action))
        {
            return Results.BadRequest("Action must be one of: pause, resume, cancel, reactivate.");
        }

        var subscription = await _subscriptionService.ChangeLifecycleForUserAsync(reference, action,
            request.EndOfPeriod, request.Reason);
        return Results.Ok(subscription.ToDto());
    }
}
