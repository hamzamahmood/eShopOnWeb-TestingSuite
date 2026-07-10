# Invoices.ReadCreditNote

_Controller: Invoices — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;CreditNote&gt; ReadCreditNote(string uid, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Use this endpoint to retrieve the details for a credit note.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Invoices.ReadCreditNote(uid);
    // TODO: Handle 'response' of type CreditNote
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
| <code>uid</code> | <code>string</code> | The unique identifier of the credit note |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[CreditNote](Models/CreditNote.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RawError](Core/ErrorResponse/RawError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
