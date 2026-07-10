# ProductPricePoints.CreateProductPricePoint

_Controller: ProductPricePoints — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ProductPricePointResponse&gt; CreateProductPricePoint(ProductIdModel productId, CreateProductPricePointRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Creates a Product Price Point. See the [Product Price Point](https://maxio.zendesk.com/hc/en-us/articles/24261111947789-Product-Price-Points) documentation for details.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.ProductPricePoints.CreateProductPricePoint(productId, body);
    // TODO: Handle 'response' of type ProductPricePointResponse
}
catch (SdkException<CreateProductPricePointError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type CreateProductPricePointError
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
| <code>productId</code> | <code>[ProductIdModel](Models/AnyOf/ProductIdModel.cs)</code> | The id or handle of the product. When using the handle, it must be prefixed with `handle:` |
| <code>body</code> | <code>[CreateProductPricePointRequest?](Models/CreateProductPricePointRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ProductPricePointResponse](Models/ProductPricePointResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[CreateProductPricePointError](Errors/CreateProductPricePointError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
