# ComponentPricePoints.CreateComponentPricePoint

_Controller: ComponentPricePoints — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ComponentPricePointResponse&gt; CreateComponentPricePoint(int componentId, CreateComponentPricePointRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Creates a price point for an existing component.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.ComponentPricePoints.CreateComponentPricePoint(componentId, body);
    // TODO: Handle 'response' of type ComponentPricePointResponse
}
catch (SdkException<CreateComponentPricePointError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type CreateComponentPricePointError
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
| <code>componentId</code> | <code>int</code> | The Advanced Billing id of the component |
| <code>body</code> | <code>[CreateComponentPricePointRequest?](Models/CreateComponentPricePointRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ComponentPricePointResponse](Models/ComponentPricePointResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[CreateComponentPricePointError](Errors/CreateComponentPricePointError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
