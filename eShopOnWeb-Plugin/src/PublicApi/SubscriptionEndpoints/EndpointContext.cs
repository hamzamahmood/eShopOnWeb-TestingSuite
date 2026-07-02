using System.Security.Claims;
using System.Threading;
using BlazorShared.Authorization;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>Builds a <see cref="SubscriptionEndpointContext"/> from the JWT principal minimal API binds.</summary>
internal static class EndpointContext
{
    public static SubscriptionEndpointContext From(ISubscriptionService subscriptionService, ClaimsPrincipal user, CancellationToken cancellationToken) => new()
    {
        SubscriptionService = subscriptionService,
        CallerUserId = user.Identity!.Name!,
        CallerIsAdmin = user.IsInRole(Constants.Roles.ADMINISTRATORS),
        CancellationToken = cancellationToken
    };
}
