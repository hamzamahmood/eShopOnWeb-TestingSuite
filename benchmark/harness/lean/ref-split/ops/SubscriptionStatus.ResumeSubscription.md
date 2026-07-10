# SubscriptionStatus.ResumeSubscription

_Controller: SubscriptionStatus — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;SubscriptionResponse&gt; ResumeSubscription(int subscriptionId, ResumptionCharge? calendarBillingResumptionCharge, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Resumes a paused (on-hold) subscription. If the normal next renewal date has not passed, the subscription will return to active and will renew on that date.  Otherwise, it will behave like a reactivation, setting the billing date to 'now' and charging the subscriber.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.SubscriptionStatus.ResumeSubscription(subscriptionId, calendarBillingResumptionCharge);
    // TODO: Handle 'response' of type SubscriptionResponse
}
catch (SdkException<ResumeSubscriptionError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type ResumeSubscriptionError
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
| <code>calendarBillingResumptionCharge</code> | <code>[ResumptionCharge?](Models/Enums/ResumptionCharge.cs)</code> | (For calendar billing subscriptions only) The way that the resumed subscription's charge should be handled. |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[SubscriptionResponse](Models/SubscriptionResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[ResumeSubscriptionError](Errors/ResumeSubscriptionError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
