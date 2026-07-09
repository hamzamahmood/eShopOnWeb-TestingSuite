using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Lists the recurring subscription plans available from the billing provider (UC1, step 1).
/// </summary>
public class ListPlansEndpoint : IEndpoint<IResult>
{
    private readonly ISubscriptionService _subscriptionService;

    public ListPlansEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/subscription-plans", async () => await HandleAsync())
            .Produces<SubscriptionPlanDto[]>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync()
    {
        var plans = await _subscriptionService.GetAvailablePlansAsync();
        return Results.Ok(plans.Select(p => p.ToDto()).ToArray());
    }
}
