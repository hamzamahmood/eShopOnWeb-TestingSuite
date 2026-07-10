# SubscriptionNotes.UpdateSubscriptionNote

_Controller: SubscriptionNotes — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;SubscriptionNoteResponse&gt; UpdateSubscriptionNote(int subscriptionId, int noteId, UpdateSubscriptionNoteRequest? body, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Updates a note for a subscription.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.SubscriptionNotes.UpdateSubscriptionNote(subscriptionId, noteId, body);
    // TODO: Handle 'response' of type SubscriptionNoteResponse
}
catch (SdkException<UpdateSubscriptionNoteError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type UpdateSubscriptionNoteError
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
| <code>noteId</code> | <code>int</code> | The Advanced Billing id of the note |
| <code>body</code> | <code>[UpdateSubscriptionNoteRequest?](Models/UpdateSubscriptionNoteRequest.cs)</code> | - |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>[SubscriptionNoteResponse](Models/SubscriptionNoteResponse.cs)</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[UpdateSubscriptionNoteError](Errors/UpdateSubscriptionNoteError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
