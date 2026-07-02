using System.ComponentModel.DataAnnotations;
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
using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class CancelRequest : BaseRequest
{
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>"Immediate" or "EndOfPeriod".</summary>
    [Required]
    public string Timing { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Reason { get; set; }
}

/// <summary>UC4: cancels a subscription, either immediately or scheduled for the end of the current period.</summary>
public class CancelSubscriptionEndpoint : IEndpoint<IResult, CancelRequest, SubscriptionEndpointContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/{subscriptionId}/cancel",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string subscriptionId, CancelBody body, ISubscriptionService subscriptionService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var request = new CancelRequest { SubscriptionId = subscriptionId, Timing = body.Timing, Reason = body.Reason };
                return await HandleAsync(request, EndpointContext.From(subscriptionService, user, cancellationToken));
            })
            .Produces<LifecycleResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(CancelRequest request, SubscriptionEndpointContext context)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        if (!System.Enum.TryParse<CancelTiming>(request.Timing, out var timing))
        {
            return Results.ValidationProblem(new System.Collections.Generic.Dictionary<string, string[]>
            {
                [nameof(request.Timing)] = new[] { "Must be 'Immediate' or 'EndOfPeriod'." }
            });
        }

        try
        {
            var subscription = await context.SubscriptionService.CancelAsync(request.SubscriptionId, context.CallerUserId, context.CallerIsAdmin, timing, request.Reason, context.CancellationToken);
            return Results.Ok(LifecycleResponse.From(request, subscription));
        }
        catch (SubscriptionNotFoundException)
        {
            return Results.NotFound();
        }
    }
}

public record CancelBody(string Timing, string? Reason);
