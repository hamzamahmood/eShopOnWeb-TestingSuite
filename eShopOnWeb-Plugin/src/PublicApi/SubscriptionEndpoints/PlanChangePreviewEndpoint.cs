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
/// Previews the prorated cost of moving the caller's subscription to another plan (UC3).
/// </summary>
public class PlanChangePreviewEndpoint : IEndpoint<IResult, PlanChangePreviewRequest, ClaimsPrincipal>
{
    private readonly ISubscriptionService _subscriptionService;

    public PlanChangePreviewEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/plan-change/preview",
                [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
                (PlanChangePreviewRequest request, ClaimsPrincipal user) => await HandleAsync(request, user))
            .Produces<PlanChangePreviewDto>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(PlanChangePreviewRequest request, ClaimsPrincipal user)
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

        var preview = await _subscriptionService.PreviewPlanChangeForUserAsync(reference, request.TargetPlanHandle,
            request.ApplyNow);
        return Results.Ok(preview.ToDto());
    }
}
