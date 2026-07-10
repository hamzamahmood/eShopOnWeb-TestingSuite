# ComponentPricePoints.UpdateCurrencyPrices

_Controller: ComponentPricePoints — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ComponentCurrencyPricesResponse&gt; UpdateCurrencyPrices(int pricePointId, UpdateCurrencyPricesRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Updates currency prices for a given currency defined at the site level.

Note: Currency Prices are not able to be updated for custom price points.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.ComponentPricePoints.UpdateCurrencyPrices(pricePointId, body);
    // TODO: Handle 'response' of type ComponentCurrencyPricesResponse
}
catch (SdkException<UpdateCurrencyPricesError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type UpdateCurrencyPricesError
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
| <code>pricePointId</code> | <code>int</code> | The Advanced Billing id of the price point |
| <code>body</code> | <code>[UpdateCurrencyPricesRequest?](Models/UpdateCurrencyPricesRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ComponentCurrencyPricesResponse](Models/ComponentCurrencyPricesResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[UpdateCurrencyPricesError](Errors/UpdateCurrencyPricesError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
