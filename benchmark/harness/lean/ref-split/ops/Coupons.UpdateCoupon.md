# Coupons.UpdateCoupon

_Controller: Coupons — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;CouponResponse&gt; UpdateCoupon(int productFamilyId, int couponId, CouponRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Updates a coupon. 

You can restrict a coupon to only apply to specific products / components by optionally passing in hashes of `restricted_products` and/or `restricted_components` in the format:
`{ "<product/component_id>": boolean_value }`

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Coupons.UpdateCoupon(productFamilyId, couponId, body);
    // TODO: Handle 'response' of type CouponResponse
}
catch (SdkException<UpdateCouponError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type UpdateCouponError
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
| <code>productFamilyId</code> | <code>int</code> | The Advanced Billing id of the product family to which the coupon belongs |
| <code>couponId</code> | <code>int</code> | The Advanced Billing id of the coupon |
| <code>body</code> | <code>[CouponRequest?](Models/CouponRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[CouponResponse](Models/CouponResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[UpdateCouponError](Errors/UpdateCouponError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
