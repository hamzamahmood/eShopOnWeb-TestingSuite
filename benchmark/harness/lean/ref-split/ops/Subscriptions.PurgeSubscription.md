# Subscriptions.PurgeSubscription

_Controller: Subscriptions — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;SubscriptionResponse&gt; PurgeSubscription(int subscriptionId, int ack, IReadOnlyList&lt;SubscriptionPurgeType&gt;? cascade, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Purges an individual subscription for sites in test mode.

Provide the subscription ID in the url.  To confirm, supply the customer ID in the query string `ack` parameter. You may also delete the customer record and/or payment profiles by passing `cascade` parameters. For example, to delete just the customer record, the query params would be: `?ack={customer_id}&cascade[]=customer`

If you need to remove subscriptions from a live site, contact support to discuss your use case.

### Delete customer and payment profile

The query params will be: `?ack={customer_id}&cascade[]=customer&cascade[]=payment_profile`

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Subscriptions.PurgeSubscription(subscriptionId, ack, cascade);
    // TODO: Handle 'response' of type SubscriptionResponse
}
catch (SdkException<PurgeSubscriptionError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type PurgeSubscriptionError
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
| <code>ack</code> | <code>int</code> | id of the customer. |
| <code>cascade</code> | <code>IReadOnlyList&lt;[SubscriptionPurgeType](Models/Enums/SubscriptionPurgeType.cs)&gt;?</code> | Options are "customer" or "payment_profile".<br>Use in query: `cascade[]=customer&cascade[]=payment_profile`. |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[SubscriptionResponse](Models/SubscriptionResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[PurgeSubscriptionError](Errors/PurgeSubscriptionError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
