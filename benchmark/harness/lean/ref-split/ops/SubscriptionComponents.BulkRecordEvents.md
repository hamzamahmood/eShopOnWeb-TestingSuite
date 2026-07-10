# SubscriptionComponents.BulkRecordEvents

_Controller: SubscriptionComponents — from the Maxio SDK API reference._

<details>
<summary><code>Task BulkRecordEvents(string apiHandle, string? storeUid, IReadOnlyList&lt;EbbEvent&gt;? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Records a collection of events.

*Note: this endpoint differs from the standard Chargify API endpoints in that the subdomain will be `events` and your site subdomain will be included in the URL path.*

A maximum of 1000 events can be published in a single request. A 422 will be returned if this limit is exceeded.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    await client.SubscriptionComponents.BulkRecordEvents(apiHandle, storeUid, body);
}
catch (SdkException<RawError> ex)
{
    // TODO: Handle 'ex.Error' of type RawError
}
```

</dd>
</dl>

### Parameters

<dl>
<dd>

| Name | Type | Description |
| --- | --- | --- |
| <code>apiHandle</code> | <code>string</code> | Identifies the Stream for which the events should be published. |
| <code>storeUid</code> | <code>string?</code> | If you've attached your own Keen project as an Advanced Billing event data-store, use this parameter to indicate the data-store. |
| <code>body</code> | <code>IReadOnlyList&lt;[EbbEvent](Models/EbbEvent.cs)&gt;?</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: No content

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RawError](Core/ErrorResponse/RawError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
