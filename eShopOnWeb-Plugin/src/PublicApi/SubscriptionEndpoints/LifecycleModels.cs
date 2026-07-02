using Microsoft.eShopWeb.ApplicationCore.Models.Subscriptions;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>Shared request shape for the parameterless lifecycle actions (pause/resume/reactivate).</summary>
public class LifecycleRequest : BaseRequest
{
    public string SubscriptionId { get; set; } = string.Empty;
}

/// <summary>Shared response shape for every lifecycle action, including cancel.</summary>
public class LifecycleResponse : BaseResponse
{
    public LifecycleResponse(System.Guid correlationId) : base(correlationId) { }
    public LifecycleResponse() { }

    public string SubscriptionId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;

    public static LifecycleResponse From(BaseRequest request, SubscriptionDto subscription) => new(request.CorrelationId())
    {
        SubscriptionId = subscription.SubscriptionId,
        State = subscription.State.ToString()
    };
}
