# SubscriptionInvoiceAccount.RefundPrepayment

_Controller: SubscriptionInvoiceAccount — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;PrepaymentResponse&gt; RefundPrepayment(int subscriptionId, long prepaymentId, RefundPrepaymentRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Refunds a prepayment applied to a subscription, either fully or partially. The `prepayment_id` will be the account transaction ID of the original payment. The prepayment must have some amount remaining in order to be refunded.

The amount may be passed either as a decimal, with `amount`, or an integer in cents, with `amount_in_cents`.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.SubscriptionInvoiceAccount.RefundPrepayment(subscriptionId, prepaymentId, body);
    // TODO: Handle 'response' of type PrepaymentResponse
}
catch (SdkException<RefundPrepaymentApiError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type RefundPrepaymentApiError
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
| <code>prepaymentId</code> | <code>long</code> | id of prepayment |
| <code>body</code> | <code>[RefundPrepaymentRequest?](Models/RefundPrepaymentRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[PrepaymentResponse](Models/PrepaymentResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RefundPrepaymentApiError](Errors/RefundPrepaymentApiError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
