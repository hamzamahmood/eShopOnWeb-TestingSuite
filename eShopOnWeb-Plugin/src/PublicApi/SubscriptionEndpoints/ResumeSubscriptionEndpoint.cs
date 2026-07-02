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

/// <summary>UC4: resumes a paused (on-hold) subscription.</summary>
public class ResumeSubscriptionEndpoint : IEndpoint<IResult, LifecycleRequest, SubscriptionEndpointContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/{subscriptionId}/resume",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string subscriptionId, ISubscriptionService subscriptionService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
                await HandleAsync(new LifecycleRequest { SubscriptionId = subscriptionId }, EndpointContext.From(subscriptionService, user, cancellationToken)))
            .Produces<LifecycleResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(LifecycleRequest request, SubscriptionEndpointContext context)
    {
        try
        {
            var subscription = await context.SubscriptionService.ResumeAsync(request.SubscriptionId, context.CallerUserId, context.CallerIsAdmin, context.CancellationToken);
            return Results.Ok(LifecycleResponse.From(request, subscription));
        }
        catch (SubscriptionNotFoundException)
        {
            return Results.NotFound();
        }
    }
}
