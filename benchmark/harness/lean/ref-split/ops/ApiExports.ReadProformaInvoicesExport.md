# ApiExports.ReadProformaInvoicesExport

_Controller: ApiExports — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;BatchJobResponse&gt; ReadProformaInvoicesExport(string batchId, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Returns a batch job object for a proforma invoices export.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.ApiExports.ReadProformaInvoicesExport(batchId);
    // TODO: Handle 'response' of type BatchJobResponse
}
catch (SdkException<ReadProformaInvoicesExportError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type ReadProformaInvoicesExportError
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
| <code>batchId</code> | <code>string</code> | Id of a Batch Job. |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[BatchJobResponse](Models/BatchJobResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[ReadProformaInvoicesExportError](Errors/ReadProformaInvoicesExportError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
