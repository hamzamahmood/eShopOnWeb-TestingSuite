# PaymentProfiles.ChangeSubscriptionGroupDefaultPaymentProfile

_Controller: PaymentProfiles — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;PaymentProfileResponse&gt; ChangeSubscriptionGroupDefaultPaymentProfile(string uid, int paymentProfileId, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

This will change the default payment profile on the subscription group to the existing payment profile with the id specified.

You must elect to change the existing payment profile to a new payment profile ID in order to receive a satisfactory response from this endpoint.

The new payment profile must belong to the subscription group's customer, otherwise you will receive an error.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.PaymentProfiles.ChangeSubscriptionGroupDefaultPaymentProfile(uid, paymentProfileId);
    // TODO: Handle 'response' of type PaymentProfileResponse
}
catch (SdkException<ChangeSubscriptionGroupDefaultPaymentProfileError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type ChangeSubscriptionGroupDefaultPaymentProfileError
    }
}
```

</dd>
</dl>

### Parameters

<dl>
<dd>

| Name | Type | Description |
| --- | --- | --- |
| <code>uid</code> | <code>string</code> | The uid of the subscription group |
| <code>paymentProfileId</code> | <code>int</code> | The Chargify id of the payment profile |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[PaymentProfileResponse](Models/PaymentProfileResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[ChangeSubscriptionGroupDefaultPaymentProfileError](Errors/ChangeSubscriptionGroupDefaultPaymentProfileError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
