using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A plan-change commit's preview token was missing, expired, or no longer matches what would be
/// previewed now. The caller must request a fresh preview rather than being silently charged a different
/// amount than what they confirmed (AC-07b).
/// </summary>
public class StalePreviewException : Exception
{
    public StalePreviewException() : base("This plan-change preview has expired or no longer matches the subscription. Request a new preview.")
    {
    }
}
