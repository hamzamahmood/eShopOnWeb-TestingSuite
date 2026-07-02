using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The configured metered-component handle does not exist, is not of metered kind, or is not on the
/// configured product family. Usage must never be sent to the provider when this is thrown (UC2 precondition).
/// </summary>
public class MeteredComponentMisconfiguredException : Exception
{
    public MeteredComponentMisconfiguredException(string handle, string reason)
        : base($"Metered component '{handle}' is misconfigured: {reason}")
    {
    }
}
