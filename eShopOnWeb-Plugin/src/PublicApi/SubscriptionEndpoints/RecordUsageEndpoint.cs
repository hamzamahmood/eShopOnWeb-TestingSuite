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
/// Records pay-as-you-go usage against any subscription's metered component (UC2).
/// Admin-guarded because it can target any subscription.
/// </summary>
public class RecordUsageEndpoint : IEndpoint<IResult, RecordUsageRequest>
{
    private readonly ISubscriptionService _subscriptionService;

    public RecordUsageEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/usage",
                [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
                (RecordUsageRequest request) => await HandleAsync(request))
            .Produces<UsageResultDto>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(RecordUsageRequest request)
    {
        if (request.SubscriptionId <= 0)
        {
            return Results.BadRequest("A valid subscription id is required.");
        }

        if (request.Quantity <= 0)
        {
            return Results.BadRequest("Quantity must be greater than zero.");
        }

        var usage = await _subscriptionService.RecordUsageAsync(request.SubscriptionId, request.Quantity, request.Memo);
        return Results.Ok(usage.ToDto());
    }
}
