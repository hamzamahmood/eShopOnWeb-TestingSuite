# Customers.UpdateCustomer

_Controller: Customers — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;CustomerResponse&gt; UpdateCustomer(int id, UpdateCustomerRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Updates the customer.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Customers.UpdateCustomer(id, body);
    // TODO: Handle 'response' of type CustomerResponse
}
catch (SdkException<UpdateCustomerError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type UpdateCustomerError
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
| <code>id</code> | <code>int</code> | The Advanced Billing id of the customer |
| <code>body</code> | <code>[UpdateCustomerRequest?](Models/UpdateCustomerRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[CustomerResponse](Models/CustomerResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[UpdateCustomerError](Errors/UpdateCustomerError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
