using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Previews the prorated cost of moving a subscription to a different plan before committing (UC3 step 2).
/// </summary>
public class PreviewPlanChangeEndpoint : IEndpoint<IResult, PlanChangeRequest, ISubscriptionService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/{subscriptionId}/plan-change/preview",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int subscriptionId, PlanChangeRequest request, ClaimsPrincipal user, ISubscriptionService subscriptionService) =>
            {
                request.SubscriptionId = subscriptionId;
                return await HandleAsync(request, subscriptionService);
            })
            .Produces<PlanChangePreviewResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(PlanChangeRequest request, ISubscriptionService subscriptionService)
    {
        var timing = ParseTiming(request.Timing);
        var preview = await subscriptionService.PreviewPlanChangeAsync(request.SubscriptionId, request.TargetProductHandle, timing);
        var response = new PlanChangePreviewResponse(request.CorrelationId()) { Preview = preview.ToDto() };
        return Results.Ok(response);
    }

    internal static PlanChangeTiming ParseTiming(string? timing) =>
        Enum.TryParse<PlanChangeTiming>(timing, ignoreCase: true, out var parsed) ? parsed : PlanChangeTiming.Immediate;
}
