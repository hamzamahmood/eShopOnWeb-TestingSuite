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

public class PreviewPlanChangeRequest : BaseRequest
{
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    public string TargetProductHandle { get; set; } = string.Empty;

    /// <summary>"Immediate" (prorated now) or "AtRenewal" (no proration).</summary>
    [Required]
    public string Timing { get; set; } = string.Empty;
}

public class PreviewPlanChangeResponse : BaseResponse
{
    public PreviewPlanChangeResponse(System.Guid correlationId) : base(correlationId) { }
    public PreviewPlanChangeResponse() { }

    public string FromProductHandle { get; set; } = string.Empty;
    public string ToProductHandle { get; set; } = string.Empty;
    public string Timing { get; set; } = string.Empty;
    public decimal ProratedAmount { get; set; }
    public System.DateTimeOffset EffectiveAt { get; set; }
    public string PreviewToken { get; set; } = string.Empty;
}

/// <summary>UC3: previews the cost of a plan change before it is committed.</summary>
public class PreviewPlanChangeEndpoint : IEndpoint<IResult, PreviewPlanChangeRequest, SubscriptionEndpointContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/{subscriptionId}/plan-change/preview",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string subscriptionId, PreviewPlanChangeBody body, ISubscriptionService subscriptionService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var request = new PreviewPlanChangeRequest { SubscriptionId = subscriptionId, TargetProductHandle = body.TargetProductHandle, Timing = body.Timing };
                return await HandleAsync(request, EndpointContext.From(subscriptionService, user, cancellationToken));
            })
            .Produces<PreviewPlanChangeResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(PreviewPlanChangeRequest request, SubscriptionEndpointContext context)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        if (!System.Enum.TryParse<PlanChangeTiming>(request.Timing, out var timing))
        {
            return Results.ValidationProblem(new System.Collections.Generic.Dictionary<string, string[]>
            {
                [nameof(request.Timing)] = new[] { "Must be 'Immediate' or 'AtRenewal'." }
            });
        }

        try
        {
            var preview = await context.SubscriptionService.PreviewPlanChangeAsync(request.SubscriptionId, context.CallerUserId, context.CallerIsAdmin, request.TargetProductHandle, timing, context.CancellationToken);
            var response = new PreviewPlanChangeResponse(request.CorrelationId())
            {
                FromProductHandle = preview.FromProductHandle,
                ToProductHandle = preview.ToProductHandle,
                Timing = preview.Timing.ToString(),
                ProratedAmount = preview.ProratedAmount,
                EffectiveAt = preview.EffectiveAt,
                PreviewToken = preview.PreviewToken
            };
            return Results.Ok(response);
        }
        catch (SubscriptionNotFoundException)
        {
            return Results.NotFound();
        }
    }
}

public record PreviewPlanChangeBody(string TargetProductHandle, string Timing);
