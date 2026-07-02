using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public record SubscriptionResponseItem(string SubscriptionId, string ProductHandle, string ProductName, decimal Price, string State, decimal Mrr, System.DateTimeOffset? NextAssessmentAt);

public class MySubscriptionsResponse : BaseResponse
{
    public MySubscriptionsResponse(System.Guid correlationId) : base(correlationId) { }
    public MySubscriptionsResponse() { }

    public List<SubscriptionResponseItem> Subscriptions { get; set; } = new();
}

/// <summary>UC1 (mine): lists only the authenticated user's own subscriptions.</summary>
public class MySubscriptionsEndpoint : IEndpoint<IResult, SubscriptionEndpointContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/subscriptions/mine",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ISubscriptionService subscriptionService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                await HandleAsync(EndpointContext.From(subscriptionService, user, cancellationToken)))
            .Produces<MySubscriptionsResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(SubscriptionEndpointContext context)
    {
        var subscriptions = await context.SubscriptionService.ListMySubscriptionsAsync(context.CallerUserId, context.CancellationToken);
        var response = new MySubscriptionsResponse
        {
            Subscriptions = subscriptions
                .Select(s => new SubscriptionResponseItem(s.SubscriptionId, s.ProductHandle, s.ProductName, s.Price, s.State.ToString(), s.Mrr, s.NextAssessmentAt))
                .ToList()
        };
        return Results.Ok(response);
    }
}
