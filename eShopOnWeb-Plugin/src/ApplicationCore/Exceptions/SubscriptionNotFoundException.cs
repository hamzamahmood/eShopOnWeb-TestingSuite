using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// No subscription with the given id exists, or it exists but does not belong to the requesting caller
/// (the two are deliberately indistinguishable to a non-admin caller, to avoid leaking existence).
/// </summary>
public class SubscriptionNotFoundException : Exception
{
    public SubscriptionNotFoundException(string subscriptionId) : base($"No subscription found with id {subscriptionId}")
    {
    }
}
