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
/// Commits a plan change with the chosen timing (UC3 step 4). The confirmed preview amounts on the
/// request are re-validated by the service, which rejects a stale preview.
/// </summary>
public class PlanChangeEndpoint : IEndpoint<IResult, PlanChangeRequest, ISubscriptionService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/{subscriptionId}/plan-change",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int subscriptionId, PlanChangeRequest request, ClaimsPrincipal user, ISubscriptionService subscriptionService) =>
            {
                request.SubscriptionId = subscriptionId;
                return await HandleAsync(request, subscriptionService);
            })
            .Produces<PlanChangeResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(PlanChangeRequest request, ISubscriptionService subscriptionService)
    {
        var timing = PreviewPlanChangeEndpoint.ParseTiming(request.Timing);
        var confirmedPreview = new ProrationPreview
        {
            TargetProductHandle = request.TargetProductHandle,
            Timing = timing,
            ProratedAdjustmentInCents = request.ProratedAdjustmentInCents,
            ChargeInCents = request.ChargeInCents,
            PaymentDueInCents = request.PaymentDueInCents,
            CreditAppliedInCents = request.CreditAppliedInCents
        };

        var updated = await subscriptionService.ChangePlanAsync(request.SubscriptionId, request.TargetProductHandle, timing, confirmedPreview);
        var response = new PlanChangeResponse(request.CorrelationId()) { Subscription = updated.ToDto() };
        return Results.Ok(response);
    }
}
