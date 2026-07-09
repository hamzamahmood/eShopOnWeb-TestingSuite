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
/// Commits a plan change for the caller's subscription (UC3). The confirmed
/// amount is re-validated against a fresh preview; a stale preview is rejected.
/// </summary>
public class PlanChangeEndpoint : IEndpoint<IResult, PlanChangeRequest, ClaimsPrincipal>
{
    private readonly ISubscriptionService _subscriptionService;

    public PlanChangeEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/plan-change",
                [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
                (PlanChangeRequest request, ClaimsPrincipal user) => await HandleAsync(request, user))
            .Produces<CustomerSubscriptionDto>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(PlanChangeRequest request, ClaimsPrincipal user)
    {
        var reference = user.Identity?.Name;
        if (string.IsNullOrEmpty(reference))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.TargetPlanHandle))
        {
            return Results.BadRequest("A target plan handle is required.");
        }

        var subscription = await _subscriptionService.ChangePlanForUserAsync(reference, request.TargetPlanHandle,
            request.ApplyNow, request.ConfirmedAmountDueInCents);
        return Results.Ok(subscription.ToDto());
    }
}
