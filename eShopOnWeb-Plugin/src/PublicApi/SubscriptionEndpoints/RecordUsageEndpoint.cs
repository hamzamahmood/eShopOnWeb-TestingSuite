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
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

public class RecordUsageRequest : BaseRequest
{
    public string SubscriptionId { get; set; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Quantity { get; set; } = 1;

    [StringLength(500)]
    public string? Memo { get; set; }

    [Required]
    public string RequestId { get; set; } = string.Empty;
}

public class RecordUsageResponse : BaseResponse
{
    public RecordUsageResponse(System.Guid correlationId) : base(correlationId) { }
    public RecordUsageResponse() { }

    public string UsageId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Memo { get; set; }
    public System.DateTimeOffset? RecordedAt { get; set; }
}

/// <summary>UC2: records usage against a subscription. Admins may target any subscription; others only their own.</summary>
public class RecordUsageEndpoint : IEndpoint<IResult, RecordUsageRequest, SubscriptionEndpointContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/{subscriptionId}/usage",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string subscriptionId, RecordUsageBody body, ISubscriptionService subscriptionService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var request = new RecordUsageRequest { SubscriptionId = subscriptionId, Quantity = body.Quantity, Memo = body.Memo, RequestId = body.RequestId };
                return await HandleAsync(request, EndpointContext.From(subscriptionService, user, cancellationToken));
            })
            .Produces<RecordUsageResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(RecordUsageRequest request, SubscriptionEndpointContext context)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        try
        {
            var usage = await context.SubscriptionService.RecordUsageAsync(
                request.SubscriptionId, context.CallerUserId, context.CallerIsAdmin, request.Quantity, request.Memo, request.RequestId, context.CancellationToken);

            var response = new RecordUsageResponse(request.CorrelationId())
            {
                UsageId = usage.UsageId,
                Quantity = usage.Quantity,
                Memo = usage.Memo,
                RecordedAt = usage.RecordedAt
            };
            return Results.Ok(response);
        }
        catch (SubscriptionNotFoundException)
        {
            return Results.NotFound();
        }
    }
}

/// <summary>The JSON body for <see cref="RecordUsageEndpoint"/> - the subscription id itself is a route segment, not part of the body.</summary>
public record RecordUsageBody(decimal Quantity, string? Memo, string RequestId);
