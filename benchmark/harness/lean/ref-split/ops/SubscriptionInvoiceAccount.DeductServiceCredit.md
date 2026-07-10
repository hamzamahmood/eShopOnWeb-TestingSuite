# SubscriptionInvoiceAccount.DeductServiceCredit

_Controller: SubscriptionInvoiceAccount — from the Maxio SDK API reference._

<details>
<summary><code>Task DeductServiceCredit(int subscriptionId, DeductServiceCreditRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Deducts a service credit from the subscription in the specified amount. The credit amount being deducted must be equal to or less than the current credit balance.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    await client.SubscriptionInvoiceAccount.DeductServiceCredit(subscriptionId, body);
}
catch (SdkException<DeductServiceCreditApiError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type DeductServiceCreditApiError
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
| <code>subscriptionId</code> | <code>int</code> | The Chargify id of the subscription. |
| <code>body</code> | <code>[DeductServiceCreditRequest?](Models/DeductServiceCreditRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: No content

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[DeductServiceCreditApiError](Errors/DeductServiceCreditApiError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
