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
/// Admin variant of UC4: applies a lifecycle action to any subscription by id.
/// </summary>
public class AdminLifecycleEndpoint : IEndpoint<IResult, AdminLifecycleRequest>
{
    private readonly ISubscriptionService _subscriptionService;

    public AdminLifecycleEndpoint(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/admin/lifecycle",
                [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
                (AdminLifecycleRequest request) => await HandleAsync(request))
            .Produces<CustomerSubscriptionDto>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(AdminLifecycleRequest request)
    {
        if (request.SubscriptionId <= 0)
        {
            return Results.BadRequest("A valid subscription id is required.");
        }

        if (!SubscriptionMappings.TryParseAction(request.Action, out var action))
        {
            return Results.BadRequest("Action must be one of: pause, resume, cancel, reactivate.");
        }

        var subscription = await _subscriptionService.ChangeLifecycleAsync(request.SubscriptionId, action,
            request.EndOfPeriod, request.Reason);
        return Results.Ok(subscription.ToDto());
    }
}
