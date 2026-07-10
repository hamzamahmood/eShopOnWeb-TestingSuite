# SubscriptionRenewals.DeleteScheduledRenewalConfigurationItem

_Controller: SubscriptionRenewals — from the Maxio SDK API reference._

<details>
<summary><code>Task DeleteScheduledRenewalConfigurationItem(int subscriptionId, int scheduledRenewalsConfigurationId, int id, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Removes an item from the pending renewal configuration.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    await client.SubscriptionRenewals.DeleteScheduledRenewalConfigurationItem(subscriptionId,
        scheduledRenewalsConfigurationId,
        id);
}
catch (SdkException<DeleteScheduledRenewalConfigurationItemError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type DeleteScheduledRenewalConfigurationItemError
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

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: No content

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[DeleteScheduledRenewalConfigurationItemError](Errors/DeleteScheduledRenewalConfigurationItemError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
