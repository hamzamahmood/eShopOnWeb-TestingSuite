# Subscriptions.RemoveCouponFromSubscription

_Controller: Subscriptions — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;string&gt; RemoveCouponFromSubscription(int subscriptionId, string? couponCode, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Removes a coupon from an existing subscription.

For more information on the expected behavior of removing a coupon from a subscription, see our documentation [here.](https://maxio.zendesk.com/hc/en-us/articles/24261259337101-Coupons-and-Subscriptions#removing-a-coupon)

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Subscriptions.RemoveCouponFromSubscription(subscriptionId, couponCode);
    // TODO: Handle 'response' of type string
}
catch (SdkException<RemoveCouponFromSubscriptionError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type RemoveCouponFromSubscriptionError
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
| <code>couponCode</code> | <code>string?</code> | The coupon code |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>string</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RemoveCouponFromSubscriptionError](Errors/RemoveCouponFromSubscriptionError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
