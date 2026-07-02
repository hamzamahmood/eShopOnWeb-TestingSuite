using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The billing provider rejected a subscribe/plan-change request with a 422 semantically-invalid response
/// that is payment-related (e.g. a declined/required payment method, or 3-D Secure). <see cref="ProviderMessages"/>
/// carries Maxio's own validation messages verbatim (UC1 failure scenario, AC-05).
/// </summary>
/// <remarks>
/// Maxio's generated 422 error body (<c>ErrorListResponse1</c>) exposes only a flat <c>errors: string[]</c> —
/// the SDK does not model a structured 3-D Secure <c>action_link</c> field despite mentioning one in prose
/// documentation on <c>CreateSubscription</c>. This is a confirmed plugin-capability gap: this exception
/// surfaces the real provider messages it *can* obtain rather than inventing a redirect URL the SDK does
/// not give it.
/// </remarks>
public class PaymentVerificationRequiredException : Exception
{
    public IReadOnlyList<string> ProviderMessages { get; }

    public PaymentVerificationRequiredException(IReadOnlyList<string> providerMessages)
        : base("Additional payment information is required before this subscription can be activated.")
    {
        ProviderMessages = providerMessages;
    }
}
