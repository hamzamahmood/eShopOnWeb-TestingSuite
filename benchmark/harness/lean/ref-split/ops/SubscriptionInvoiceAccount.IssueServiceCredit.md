# SubscriptionInvoiceAccount.IssueServiceCredit

_Controller: SubscriptionInvoiceAccount — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;ServiceCredit&gt; IssueServiceCredit(int subscriptionId, IssueServiceCreditRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Adds a service credit to the subscription in the specified amount. The credit is subsequently applied to the next generated invoice.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.SubscriptionInvoiceAccount.IssueServiceCredit(subscriptionId, body);
    // TODO: Handle 'response' of type ServiceCredit
}
catch (SdkException<IssueServiceCreditApiError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type IssueServiceCreditApiError
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
| <code>body</code> | <code>[IssueServiceCreditRequest?](Models/IssueServiceCreditRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[ServiceCredit](Models/ServiceCredit.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[IssueServiceCreditApiError](Errors/IssueServiceCreditApiError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
