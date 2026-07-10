# ProformaInvoices.ListSubscriptionGroupProformaInvoices

_Controller: ProformaInvoices — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ListProformaInvoicesResponse&gt; ListSubscriptionGroupProformaInvoices(string uid, bool? lineItems = false, bool? discounts = false, bool? taxes = false, bool? credits = false, bool? payments = false, bool? customFields = false, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Lists proforma invoices with a `consolidation_level` of parent for the subscription group.

By default, proforma invoices returned on the index will only include totals, not detailed breakdowns for `line_items`, `discounts`, `taxes`, `credits`, `payments`, `custom_fields`. To include breakdowns, pass the specific field as a key in the query with a value set to true.


</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.ProformaInvoices.ListSubscriptionGroupProformaInvoices(uid);
    // TODO: Handle 'response' of type ListProformaInvoicesResponse
}
catch (SdkException<ListSubscriptionGroupProformaInvoicesError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type ListSubscriptionGroupProformaInvoicesError
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
| <code>uid</code> | <code>string</code> | The uid of the subscription group |
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

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[ListSubscriptionGroupProformaInvoicesError](Errors/ListSubscriptionGroupProformaInvoicesError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
