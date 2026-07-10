# CustomFields.DeleteMetadata

_Controller: CustomFields — from the Maxio SDK API reference._

<details>
<summary><code>Task DeleteMetadata(ResourceType resourceType, int resourceId, string? name, IReadOnlyList&lt;string&gt;? names, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Deletes one or more metafields (and associated metadata) from the specified subscription or customer.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    await client.CustomFields.DeleteMetadata(resourceType, resourceId, name, names);
}
catch (SdkException<DeleteMetadataError> ex)
{
    if (ex.Error.TryGetError(out var error))
    {
        // TODO: Handle 'error' of type DeleteMetadataError
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
| <code>resourceType</code> | <code>[ResourceType](Models/Enums/ResourceType.cs)</code> | The resource type to which the metafields belong. |
| <code>resourceId</code> | <code>int</code> | The Advanced Billing id of the customer or the subscription for which the metadata applies |
| <code>name</code> | <code>string?</code> | Name of field to be removed. |
| <code>names</code> | <code>IReadOnlyList&lt;string&gt;?</code> | Names of fields to be removed. Use in query: `names[]=field1&names[]=my-field&names[]=another-field`. |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: No content

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[DeleteMetadataError](Errors/DeleteMetadataError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
