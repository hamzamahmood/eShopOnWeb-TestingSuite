# Invoices.ListInvoices

_Controller: Invoices — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ListInvoicesResponse&gt; ListInvoices(string? startDate, string? endDate, InvoiceStatus? status, int? subscriptionId, string? subscriptionGroupUid, string? consolidationLevel, Direction? direction, InvoiceDateField? dateField, string? startDatetime, string? endDatetime, IReadOnlyList&lt;int&gt;? customerIds, IReadOnlyList&lt;string&gt;? number, IReadOnlyList&lt;int&gt;? productIds, InvoiceSortField? sort, int? page = 1, int? perPage = 20, bool? lineItems = false, bool? discounts = false, bool? taxes = false, bool? credits = false, bool? payments = false, bool? customFields = false, bool? refunds = false, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

By default, invoices returned on the index will only include totals, not detailed breakdowns for `line_items`, `discounts`, `taxes`, `credits`, `payments`, `custom_fields`, or `refunds`. To include breakdowns, pass the specific field as a key in the query with a value set to `true`.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Invoices.ListInvoices(startDate,
        endDate,
        status,
        subscriptionId,
        subscriptionGroupUid,
        consolidationLevel,
        direction,
        dateField,
        startDatetime,
        endDatetime,
        customerIds,
        number,
        productIds,
        sort);
    // TODO: Handle 'response' of type ListInvoicesResponse
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
| <code>startDate</code> | <code>string?</code> | The start date (format YYYY-MM-DD) with which to filter the date_field. Returns invoices with a timestamp at or after midnight (12:00:00 AM) in your site’s time zone on the date specified. |
| <code>endDate</code> | <code>string?</code> | The end date (format YYYY-MM-DD) with which to filter the date_field. Returns invoices with a timestamp up to and including 11:59:59PM in your site’s time zone on the date specified. |
| <code>status</code> | <code>[InvoiceStatus?](Models/Enums/InvoiceStatus.cs)</code> | The current status of the invoice.  Allowed Values: draft, open, paid, pending, voided |
| <code>subscriptionId</code> | <code>int?</code> | The subscription's ID. |
| <code>subscriptionGroupUid</code> | <code>string?</code> | The UID of the subscription group you want to fetch consolidated invoices for. This will return a paginated list of consolidated invoices for the specified group. |
| <code>consolidationLevel</code> | <code>string?</code> | The consolidation level of the invoice. Allowed Values: none, parent, child or comma-separated lists of thereof, e.g. none,parent. |
| <code>direction</code> | <code>[Direction?](Models/Enums/Direction.cs)</code> | The sort direction of the returned invoices. |
| <code>dateField</code> | <code>[InvoiceDateField?](Models/Enums/InvoiceDateField.cs)</code> | The type of filter you would like to apply to your search. Use in query `date_field=issue_date`. |
| <code>startDatetime</code> | <code>string?</code> | The start date and time (format YYYY-MM-DD HH:MM:SS) with which to filter the date_field. Returns invoices with a timestamp at or after exact time provided in query. You can specify timezone in query - otherwise your site's time zone will be used. If provided, this parameter will be used instead of start_date. Allowed to be used only along with date_field set to created_at or updated_at. |
| <code>endDatetime</code> | <code>string?</code> | The end date and time (format YYYY-MM-DD HH:MM:SS) with which to filter the date_field. Returns invoices with a timestamp at or before exact time provided in query. You can specify timezone in query - otherwise your site's time zone will be used. If provided, this parameter will be used instead of end_date. Allowed to be used only along with date_field set to created_at or updated_at. |
| <code>customerIds</code> | <code>IReadOnlyList&lt;int&gt;?</code> | Allows fetching invoices with matching customer id based on provided values. Use in query `customer_ids=1,2,3`. |
| <code>number</code> | <code>IReadOnlyList&lt;string&gt;?</code> | Allows fetching invoices with matching invoice number based on provided values. Use in query `number=1234,1235`. |
| <code>productIds</code> | <code>IReadOnlyList&lt;int&gt;?</code> | Allows fetching invoices with matching line items product ids based on provided values. Use in query `product_ids=23,34`. |
| <code>sort</code> | <code>[InvoiceSortField?](Models/Enums/InvoiceSortField.cs)</code> | Allows specification of the order of the returned list. Use in query `sort=total_amount`. |
| <code>page</code> | <code>int?</code> | Result records are organized in pages. By default, the first page of results is displayed. The page parameter specifies a page number of results to fetch. You can start navigating through the pages to consume the results. You do this by passing in a page parameter. Retrieve the next page by adding ?page=2 to the query string. If there are no results to return, then an empty result set will be returned.<br>Use in query `page=1`.<br>**Default**: 1 |
| <code>perPage</code> | <code>int?</code> | This parameter indicates how many records to fetch in each request. Default value is 20. The maximum allowed values is 200; any per_page value over 200 will be changed to 200.<br>Use in query `per_page=200`.<br>**Default**: 20 |
| <code>lineItems</code> | <code>bool?</code> | Include line items data<br>**Default**: false |
| <code>discounts</code> | <code>bool?</code> | Include discounts data<br>**Default**: false |
| <code>taxes</code> | <code>bool?</code> | Include taxes data<br>**Default**: false |
| <code>credits</code> | <code>bool?</code> | Include credits data<br>**Default**: false |
| <code>payments</code> | <code>bool?</code> | Include payments data<br>**Default**: false |
| <code>customFields</code> | <code>bool?</code> | Include custom fields data<br>**Default**: false |
| <code>refunds</code> | <code>bool?</code> | Include refunds data<br>**Default**: false |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ListInvoicesResponse](Models/ListInvoicesResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RawError](Core/ErrorResponse/RawError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
