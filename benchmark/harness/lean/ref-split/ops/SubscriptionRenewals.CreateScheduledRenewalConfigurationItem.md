# SubscriptionRenewals.CreateScheduledRenewalConfigurationItem

_Controller: SubscriptionRenewals — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ScheduledRenewalConfigurationItemResponse&gt; CreateScheduledRenewalConfigurationItem(int subscriptionId, int scheduledRenewalsConfigurationId, ScheduledRenewalConfigurationItemRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Adds product and component line items to the scheduled renewal.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.SubscriptionRenewals.CreateScheduledRenewalConfigurationItem(subscriptionId,
        scheduledRenewalsConfigurationId,
        body);
    // TODO: Handle 'response' of type ScheduledRenewalConfigurationItemResponse
}
catch (SdkException<CreateScheduledRenewalConfigurationItemError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type CreateScheduledRenewalConfigurationItemError
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
| <code>scheduledRenewalsConfigurationId</code> | <code>int</code> | The scheduled renewal configuration id. |
| <code>body</code> | <code>[ScheduledRenewalConfigurationItemRequest?](Models/ScheduledRenewalConfigurationItemRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ScheduledRenewalConfigurationItemResponse](Models/ScheduledRenewalConfigurationItemResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[CreateScheduledRenewalConfigurationItemError](Errors/CreateScheduledRenewalConfigurationItemError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
