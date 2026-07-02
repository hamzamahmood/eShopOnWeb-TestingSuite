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

public class CommitPlanChangeRequest : BaseRequest
{
    public string SubscriptionId { get; set; } = string.Empty;

    [Required]
    public string PreviewToken { get; set; } = string.Empty;
}

public class CommitPlanChangeResponse : BaseResponse
{
    public CommitPlanChangeResponse(System.Guid correlationId) : base(correlationId) { }
    public CommitPlanChangeResponse() { }

    public string SubscriptionId { get; set; } = string.Empty;
    public string ProductHandle { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

/// <summary>UC3: commits a previously previewed plan change. Rejects with 409 if the preview is stale.</summary>
public class CommitPlanChangeEndpoint : IEndpoint<IResult, CommitPlanChangeRequest, SubscriptionEndpointContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions/{subscriptionId}/plan-change",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string subscriptionId, CommitPlanChangeBody body, ISubscriptionService subscriptionService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var request = new CommitPlanChangeRequest { SubscriptionId = subscriptionId, PreviewToken = body.PreviewToken };
                return await HandleAsync(request, EndpointContext.From(subscriptionService, user, cancellationToken));
            })
            .Produces<CommitPlanChangeResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(CommitPlanChangeRequest request, SubscriptionEndpointContext context)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        try
        {
            var subscription = await context.SubscriptionService.CommitPlanChangeAsync(request.SubscriptionId, context.CallerUserId, context.CallerIsAdmin, request.PreviewToken, context.CancellationToken);
            var response = new CommitPlanChangeResponse(request.CorrelationId())
            {
                SubscriptionId = subscription.SubscriptionId,
                ProductHandle = subscription.ProductHandle,
                State = subscription.State.ToString()
            };
            return Results.Ok(response);
        }
        catch (SubscriptionNotFoundException)
        {
            return Results.NotFound();
        }
    }
}

public record CommitPlanChangeBody(string PreviewToken);
