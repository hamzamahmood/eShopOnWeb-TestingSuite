# Coupons.FindCoupon

_Controller: Coupons — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;CouponResponse&gt; FindCoupon(int? productFamilyId, string? code, bool? currencyPrices, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Searches for a coupon by code, returning a 404 if no coupon is found. By passing a code parameter, the find will attempt to locate a coupon that matches that code.

If you have more than one product family and if the coupon you are trying to find does not belong to the default product family in your site, then you will need to specify (either in the url or as a query string param) the product family id.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Coupons.FindCoupon(productFamilyId, code, currencyPrices);
    // TODO: Handle 'response' of type CouponResponse
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
| <code>productFamilyId</code> | <code>int?</code> | The Advanced Billing id of the product family to which the coupon belongs |
| <code>code</code> | <code>string?</code> | The code of the coupon |
| <code>currencyPrices</code> | <code>bool?</code> | When fetching coupons, if you have defined multiple currencies at the site level, you can optionally pass the `?currency_prices=true` query param to include an array of currency price data in the response. |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[CouponResponse](Models/CouponResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RawError](Core/ErrorResponse/RawError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
