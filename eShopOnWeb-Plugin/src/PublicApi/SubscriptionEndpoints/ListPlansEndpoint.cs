using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public record PlanResponseItem(string Handle, string Name, decimal Price, int IntervalCount, string IntervalUnit, bool RequiresPaymentMethod);

public class ListPlansResponse : BaseResponse
{
    public ListPlansResponse(System.Guid correlationId) : base(correlationId) { }
    public ListPlansResponse() { }

    public List<PlanResponseItem> Plans { get; set; } = new();
}

/// <summary>
/// UC1: lists the recurring plans available for subscription. Anonymous - browsing plans requires no auth.
/// </summary>
public class ListPlansEndpoint : IEndpoint<IResult, AnonymousSubscriptionEndpointContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/subscriptions/plans",
            async (ISubscriptionService subscriptionService, CancellationToken cancellationToken) =>
                await HandleAsync(new AnonymousSubscriptionEndpointContext
                {
                    SubscriptionService = subscriptionService,
                    CancellationToken = cancellationToken
                }))
            .Produces<ListPlansResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(AnonymousSubscriptionEndpointContext context)
    {
        var plans = await context.SubscriptionService.ListPlansAsync(context.CancellationToken);
        var response = new ListPlansResponse
        {
            Plans = plans.Select(p => new PlanResponseItem(p.Handle, p.Name, p.Price, p.IntervalCount, p.IntervalUnit, p.RequiresPaymentMethod)).ToList()
        };
        return Results.Ok(response);
    }
}
