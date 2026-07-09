using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

// Raised when the integration is misconfigured against the billing provider —
// a missing API key, or a configured handle/id that no longer resolves (e.g. the
// sandbox was reseeded with new ids). Points the operator back at the seed step
// (plan UC0) rather than enrolling against a guessed plan.
public class BillingConfigurationException : Exception
{
    public BillingConfigurationException(string message) : base(message)
    {
    }

    public BillingConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
