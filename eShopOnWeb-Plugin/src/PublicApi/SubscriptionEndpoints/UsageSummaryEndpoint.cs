using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class UsageSummaryRequest : BaseRequest
{
    public string SubscriptionId { get; set; } = string.Empty;
}

public class UsageSummaryResponse : BaseResponse
{
    public UsageSummaryResponse(System.Guid correlationId) : base(correlationId) { }
    public UsageSummaryResponse() { }

    public string ComponentHandle { get; set; } = string.Empty;
    public decimal PeriodToDateQuantity { get; set; }
    public System.DateTimeOffset? PeriodStartsAt { get; set; }
    public System.DateTimeOffset? PeriodEndsAt { get; set; }
}

/// <summary>UC2: reads the period-to-date usage total for a subscription.</summary>
public class UsageSummaryEndpoint : IEndpoint<IResult, UsageSummaryRequest, SubscriptionEndpointContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/subscriptions/{subscriptionId}/usage",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string subscriptionId, ISubscriptionService subscriptionService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                await HandleAsync(new UsageSummaryRequest { SubscriptionId = subscriptionId }, EndpointContext.From(subscriptionService, user, cancellationToken)))
            .Produces<UsageSummaryResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(UsageSummaryRequest request, SubscriptionEndpointContext context)
    {
        try
        {
            var summary = await context.SubscriptionService.GetUsageSummaryAsync(request.SubscriptionId, context.CallerUserId, context.CallerIsAdmin, context.CancellationToken);
            var response = new UsageSummaryResponse(request.CorrelationId())
            {
                ComponentHandle = summary.ComponentHandle,
                PeriodToDateQuantity = summary.PeriodToDateQuantity,
                PeriodStartsAt = summary.PeriodStartsAt,
                PeriodEndsAt = summary.PeriodEndsAt
            };
            return Results.Ok(response);
        }
        catch (SubscriptionNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
