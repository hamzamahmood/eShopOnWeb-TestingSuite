using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

// Raised when the billing provider rejects a request or is unreachable. The
// message is safe to surface to callers; the storefront and PublicApi translate
// it into a friendly error without rolling back eShopOnWeb's own state.
public class BillingProviderException : Exception
{
    public BillingProviderException(string message) : base(message)
    {
    }

    public BillingProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
