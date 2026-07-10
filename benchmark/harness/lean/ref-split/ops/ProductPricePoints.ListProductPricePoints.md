# ProductPricePoints.ListProductPricePoints

_Controller: ProductPricePoints — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ListProductPricePointsResponse&gt; ListProductPricePoints(ProductIdModel productId, bool? currencyPrices, IReadOnlyList&lt;PricePointType&gt;? filterType, bool? archived, int? page = 1, int? perPage = 10, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Retrieves a list of product price points.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.ProductPricePoints.ListProductPricePoints(productId,
        currencyPrices,
        filterType,
        archived);
    // TODO: Handle 'response' of type ListProductPricePointsResponse
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
| <code>productId</code> | <code>[ProductIdModel](Models/AnyOf/ProductIdModel.cs)</code> | The id or handle of the product. When using the handle, it must be prefixed with `handle:` |
| <code>currencyPrices</code> | <code>bool?</code> | When fetching a product's price points, if you have defined multiple currencies at the site level, you can optionally pass the ?currency_prices=true query param to include an array of currency price data in the response. If the product price point is set to use_site_exchange_rate: true, it will return pricing based on the current exchange rate. If the flag is set to false, it will return all of the defined prices for each currency. |
| <code>filterType</code> | <code>IReadOnlyList&lt;[PricePointType](Models/Enums/PricePointType.cs)&gt;?</code> | Use in query: `filter[type]=catalog,default`. |
| <code>archived</code> | <code>bool?</code> | Set to include archived price points in the response. |
| <code>page</code> | <code>int?</code> | Result records are organized in pages. By default, the first page of results is displayed. The page parameter specifies a page number of results to fetch. You can start navigating through the pages to consume the results. You do this by passing in a page parameter. Retrieve the next page by adding ?page=2 to the query string. If there are no results to return, then an empty result set will be returned.<br>Use in query `page=1`.<br>**Default**: 1 |
| <code>perPage</code> | <code>int?</code> | This parameter indicates how many records to fetch in each request. Default value is 10. The maximum allowed values is 200; any per_page value over 200 will be changed to 200.<br>**Default**: 10 |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ListProductPricePointsResponse](Models/ListProductPricePointsResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RawError](Core/ErrorResponse/RawError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
