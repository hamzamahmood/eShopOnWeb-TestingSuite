# ProformaInvoices.ListProformaInvoices

_Controller: ProformaInvoices — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ListProformaInvoicesResponse&gt; ListProformaInvoices(int subscriptionId, string? startDate, string? endDate, ProformaInvoiceStatus? status, Direction? direction, int? page = 1, int? perPage = 20, bool? lineItems = false, bool? discounts = false, bool? taxes = false, bool? credits = false, bool? payments = false, bool? customFields = false, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Lists proforma invoices for a subscription. By default, results only include totals, not detailed breakdowns for `line_items`, `discounts`, `taxes`, `credits`, `payments`, or `custom_fields`. To include breakdowns, pass the specific field as a key in the query with a value set to `true`.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.ProformaInvoices.ListProformaInvoices(subscriptionId,
        startDate,
        endDate,
        status,
        direction);
    // TODO: Handle 'response' of type ListProformaInvoicesResponse
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
| <code>subscriptionId</code> | <code>int</code> | The Chargify id of the subscription. |
| <code>startDate</code> | <code>string?</code> | The beginning date range for the invoice's Due Date, in the YYYY-MM-DD format. |
| <code>endDate</code> | <code>string?</code> | The ending date range for the invoice's Due Date, in the YYYY-MM-DD format. |
| <code>status</code> | <code>[ProformaInvoiceStatus?](Models/Enums/ProformaInvoiceStatus.cs)</code> | The current status of the invoice.  Allowed Values: draft, open, paid, pending, voided |
| <code>direction</code> | <code>[Direction?](Models/Enums/Direction.cs)</code> | The sort direction of the returned invoices. |
| <code>page</code> | <code>int?</code> | Result records are organized in pages. By default, the first page of results is displayed. The page parameter specifies a page number of results to fetch. You can start navigating through the pages to consume the results. You do this by passing in a page parameter. Retrieve the next page by adding ?page=2 to the query string. If there are no results to return, then an empty result set will be returned.<br>Use in query `page=1`.<br>**Default**: 1 |
| <code>perPage</code> | <code>int?</code> | This parameter indicates how many records to fetch in each request. Default value is 20. The maximum allowed values is 200; any per_page value over 200 will be changed to 200.<br>Use in query `per_page=200`.<br>**Default**: 20 |
| <code>lineItems</code> | <code>bool?</code> | Include line items data<br>**Default**: false |
| <code>discounts</code> | <code>bool?</code> | Include discounts data<br>**Default**: false |
| <code>taxes</code> | <code>bool?</code> | Include taxes data<br>**Default**: false |
| <code>credits</code> | <code>bool?</code> | Include credits data<br>**Default**: false |
| <code>payments</code> | <code>bool?</code> | Include payments data<br>**Default**: false |
| <code>customFields</code> | <code>bool?</code> | Include custom fields data<br>**Default**: false |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ListProformaInvoicesResponse](Models/ListProformaInvoicesResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RawError](Core/ErrorResponse/RawError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
