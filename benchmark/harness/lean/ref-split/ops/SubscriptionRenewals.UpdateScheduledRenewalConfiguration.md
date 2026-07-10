# SubscriptionRenewals.UpdateScheduledRenewalConfiguration

_Controller: SubscriptionRenewals — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ScheduledRenewalConfigurationResponse&gt; UpdateScheduledRenewalConfiguration(int subscriptionId, int id, ScheduledRenewalConfigurationRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Updates an existing configuration.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.SubscriptionRenewals.UpdateScheduledRenewalConfiguration(subscriptionId, id, body);
    // TODO: Handle 'response' of type ScheduledRenewalConfigurationResponse
}
catch (SdkException<UpdateScheduledRenewalConfigurationError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type UpdateScheduledRenewalConfigurationError
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
| <code>id</code> | <code>int</code> | The renewal id. |
| <code>body</code> | <code>[ScheduledRenewalConfigurationRequest?](Models/ScheduledRenewalConfigurationRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ScheduledRenewalConfigurationResponse](Models/ScheduledRenewalConfigurationResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[UpdateScheduledRenewalConfigurationError](Errors/UpdateScheduledRenewalConfigurationError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
