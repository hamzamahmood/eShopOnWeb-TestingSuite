# Insights.ReadMrr

_Controller: Insights — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;MrrResponse&gt; ReadMrr(DateTimeOffset? atTime, int? subscriptionId, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Returns your site's current MRR, including plan and usage breakouts.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Insights.ReadMrr(atTime, subscriptionId);
    // TODO: Handle 'response' of type MrrResponse
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
| <code>atTime</code> | <code>DateTimeOffset?</code> | submit a timestamp in ISO8601 format to request MRR for a historic time |
| <code>subscriptionId</code> | <code>int?</code> | submit the id of a subscription in order to limit results |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[MrrResponse](Models/MrrResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RawError](Core/ErrorResponse/RawError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
