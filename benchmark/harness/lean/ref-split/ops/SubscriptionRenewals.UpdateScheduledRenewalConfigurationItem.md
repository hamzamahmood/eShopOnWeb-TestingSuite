# SubscriptionRenewals.UpdateScheduledRenewalConfigurationItem

_Controller: SubscriptionRenewals — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ScheduledRenewalConfigurationItemResponse&gt; UpdateScheduledRenewalConfigurationItem(int subscriptionId, int scheduledRenewalsConfigurationId, int id, ScheduledRenewalUpdateRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Updates an existing configuration item’s pricing and quantity.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.SubscriptionRenewals.UpdateScheduledRenewalConfigurationItem(subscriptionId,
        scheduledRenewalsConfigurationId,
        id,
        body);
    // TODO: Handle 'response' of type ScheduledRenewalConfigurationItemResponse
}
catch (SdkException<UpdateScheduledRenewalConfigurationItemError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type UpdateScheduledRenewalConfigurationItemError
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
| <code>id</code> | <code>int</code> | The scheduled renewal configuration item id. |
| <code>body</code> | <code>[ScheduledRenewalUpdateRequest?](Models/ScheduledRenewalUpdateRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ScheduledRenewalConfigurationItemResponse](Models/ScheduledRenewalConfigurationItemResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[UpdateScheduledRenewalConfigurationItemError](Errors/UpdateScheduledRenewalConfigurationItemError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
