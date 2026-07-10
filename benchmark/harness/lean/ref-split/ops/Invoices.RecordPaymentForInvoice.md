# Invoices.RecordPaymentForInvoice

_Controller: Invoices — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;Invoice&gt; RecordPaymentForInvoice(string uid, CreateInvoicePaymentRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Applies a payment of a given type against a specific invoice. If you would like to apply a payment across multiple invoices, you can use the Bulk Payment endpoint.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Invoices.RecordPaymentForInvoice(uid, body);
    // TODO: Handle 'response' of type Invoice
}
catch (SdkException<RecordPaymentForInvoiceError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type RecordPaymentForInvoiceError
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
| <code>uid</code> | <code>string</code> | The unique identifier for the invoice, this does not refer to the public facing invoice number. |
| <code>body</code> | <code>[CreateInvoicePaymentRequest?](Models/CreateInvoicePaymentRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[Invoice](Models/Invoice.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RecordPaymentForInvoiceError](Errors/RecordPaymentForInvoiceError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
