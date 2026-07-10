# AdvanceInvoice.IssueAdvanceInvoice

_Controller: AdvanceInvoice — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;Invoice&gt; IssueAdvanceInvoice(int subscriptionId, IssueAdvanceInvoiceRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Generate an invoice in advance for a subscription's next renewal date. [See our docs](https://maxio.zendesk.com/hc/en-us/articles/24252026404749-Issue-Invoice-In-Advance) for more information on advance invoices, including eligibility for generating one; for the most part, they function like any other invoice, except they are issued early and have special behavior upon being voided.
A subscription may only have one advance invoice per billing period. Attempting to issue an advance invoice when one already exists will return an error.
That said, regeneration of the invoice may be forced with the params `force: true`, which will void an advance invoice if one exists and generate a new one. If no advance invoice exists, a new one will be generated.
We recommend using either the create or preview endpoints for proforma invoices to preview this advance invoice before using this endpoint to generate it.


</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.AdvanceInvoice.IssueAdvanceInvoice(subscriptionId, body);
    // TODO: Handle 'response' of type Invoice
}
catch (SdkException<IssueAdvanceInvoiceError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type IssueAdvanceInvoiceError
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
| <code>subscriptionId</code> | <code>int</code> | The Chargify id of the subscription. |
| <code>body</code> | <code>[IssueAdvanceInvoiceRequest?](Models/IssueAdvanceInvoiceRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[Invoice](Models/Invoice.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[IssueAdvanceInvoiceError](Errors/IssueAdvanceInvoiceError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
