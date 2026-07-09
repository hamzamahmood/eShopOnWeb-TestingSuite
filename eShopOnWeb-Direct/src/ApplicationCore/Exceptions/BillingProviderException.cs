using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Raised when the billing provider rejects a request or is unreachable. Carries the provider's
/// message (and HTTP status where available) so callers can surface a friendly error without
/// depending on the provider's own exception types.
/// </summary>
public class BillingProviderException : Exception
{
    public int? StatusCode { get; }

    public BillingProviderException(string message) : base(message)
    {
    }

    public BillingProviderException(string message, int? statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public BillingProviderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
