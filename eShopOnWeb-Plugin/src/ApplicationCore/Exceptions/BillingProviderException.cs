using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The billing provider could not complete a request (unexpected error, timeout, or an undeclared response).
/// The message is always safe to surface to a caller; provider-internal detail (raw error body, correlation id)
/// must be logged at the throw site, not carried on this exception.
/// </summary>
public class BillingProviderException : Exception
{
    public BillingProviderException(string message) : base(message)
    {
    }

    public BillingProviderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
