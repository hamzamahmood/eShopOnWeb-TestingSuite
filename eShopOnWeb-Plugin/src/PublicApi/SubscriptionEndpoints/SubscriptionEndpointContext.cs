using System.Threading;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// The single <c>TDependency</c> every authenticated subscription endpoint's <c>HandleAsync</c> receives:
/// the scoped service, the request's <see cref="CancellationToken"/> (minimal API's special-cased binding
/// for <c>HttpContext.RequestAborted</c>), and the caller identity resolved from the JWT in
/// <c>AddRoute</c> (the JWT carries only <c>ClaimTypes.Name</c> + <c>ClaimTypes.Role</c> - see
/// <c>IdentityTokenClaimService</c> - so the caller's reference is their username/email, matching the same
/// value the Web host uses).
/// </summary>
public class SubscriptionEndpointContext
{
    public required ISubscriptionService SubscriptionService { get; init; }
    public required string CallerUserId { get; init; }
    public required bool CallerIsAdmin { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

/// <summary>The <c>TDependency</c> for the one anonymous subscription endpoint (listing plans).</summary>
public class AnonymousSubscriptionEndpointContext
{
    public required ISubscriptionService SubscriptionService { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
