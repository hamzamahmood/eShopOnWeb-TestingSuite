# Invoices.ListCreditNotes

_Controller: Invoices — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ListCreditNotesResponse&gt; ListCreditNotes(int? subscriptionId, int? page = 1, int? perPage = 20, bool? lineItems = false, bool? discounts = false, bool? taxes = false, bool? refunds = false, bool? applications = false, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Credit Notes are like inverse invoices. They reduce the amount a customer owes.

By default, the credit notes returned by this endpoint will exclude the arrays of `line_items`, `discounts`, `taxes`, `applications`, or `refunds`. To include these arrays, pass the specific field as a key in the query with a value set to `true`.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Invoices.ListCreditNotes(subscriptionId);
    // TODO: Handle 'response' of type ListCreditNotesResponse
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
| <code>subscriptionId</code> | <code>int?</code> | The subscription's Advanced Billing id |
| <code>page</code> | <code>int?</code> | Result records are organized in pages. By default, the first page of results is displayed. The page parameter specifies a page number of results to fetch. You can start navigating through the pages to consume the results. You do this by passing in a page parameter. Retrieve the next page by adding ?page=2 to the query string. If there are no results to return, then an empty result set will be returned.<br>Use in query `page=1`.<br>**Default**: 1 |
| <code>perPage</code> | <code>int?</code> | This parameter indicates how many records to fetch in each request. Default value is 20. The maximum allowed values is 200; any per_page value over 200 will be changed to 200.<br>Use in query `per_page=200`.<br>**Default**: 20 |
| <code>lineItems</code> | <code>bool?</code> | Include line items data<br>**Default**: false |
| <code>discounts</code> | <code>bool?</code> | Include discounts data<br>**Default**: false |
| <code>taxes</code> | <code>bool?</code> | Include taxes data<br>**Default**: false |
| <code>refunds</code> | <code>bool?</code> | Include refunds data<br>**Default**: false |
| <code>applications</code> | <code>bool?</code> | Include applications data<br>**Default**: false |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ListCreditNotesResponse](Models/ListCreditNotesResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RawError](Core/ErrorResponse/RawError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
