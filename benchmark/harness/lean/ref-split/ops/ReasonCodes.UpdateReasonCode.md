# ReasonCodes.UpdateReasonCode

_Controller: ReasonCodes — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ReasonCodeResponse&gt; UpdateReasonCode(int reasonCodeId, UpdateReasonCodeRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Updates an existing reason code for a given site.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.ReasonCodes.UpdateReasonCode(reasonCodeId, body);
    // TODO: Handle 'response' of type ReasonCodeResponse
}
catch (SdkException<UpdateReasonCodeError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type UpdateReasonCodeError
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
| <code>reasonCodeId</code> | <code>int</code> | The Advanced Billing id of the reason code |
| <code>body</code> | <code>[UpdateReasonCodeRequest?](Models/UpdateReasonCodeRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ReasonCodeResponse](Models/ReasonCodeResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[UpdateReasonCodeError](Errors/UpdateReasonCodeError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
