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

public class SubscribeRequest : BaseRequest
{
    [Required]
    public string ProductHandle { get; set; } = string.Empty;
}

public class SubscribeResponse : BaseResponse
{
    public SubscribeResponse(System.Guid correlationId) : base(correlationId) { }
    public SubscribeResponse() { }

    public string SubscriptionId { get; set; } = string.Empty;
    public string ProductHandle { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string State { get; set; } = string.Empty;
    public System.DateTimeOffset? NextAssessmentAt { get; set; }
}

/// <summary>UC1: enrolls the authenticated user in a plan.</summary>
public class SubscribeEndpoint : IEndpoint<IResult, SubscribeRequest, SubscriptionEndpointContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/subscriptions",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SubscribeRequest request, ISubscriptionService subscriptionService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                return await HandleAsync(request, EndpointContext.From(subscriptionService, user, cancellationToken));
            })
            .Produces<SubscribeResponse>()
            .WithTags("SubscriptionEndpoints");
    }

    public async Task<IResult> HandleAsync(SubscribeRequest request, SubscriptionEndpointContext context)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        var response = new SubscribeResponse(request.CorrelationId());

        try
        {
            var subscription = await context.SubscriptionService.SubscribeAsync(
                context.CallerUserId, context.CallerUserId, null, null, request.ProductHandle, context.CancellationToken);

            response.SubscriptionId = subscription.SubscriptionId;
            response.ProductHandle = subscription.ProductHandle;
            response.ProductName = subscription.ProductName;
            response.Price = subscription.Price;
            response.State = subscription.State.ToString();
            response.NextAssessmentAt = subscription.NextAssessmentAt;

            return Results.Created($"api/subscriptions/{subscription.SubscriptionId}", response);
        }
        catch (PaymentVerificationRequiredException ex)
        {
            return Results.UnprocessableEntity(new { ex.Message, ex.ProviderMessages });
        }
    }
}
