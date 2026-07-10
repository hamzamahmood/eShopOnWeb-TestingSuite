# SubscriptionComponents.ListSubscriptionComponents

_Controller: SubscriptionComponents — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;IReadOnlyList&lt;SubscriptionComponentResponse&gt;&gt; ListSubscriptionComponents(int subscriptionId, SubscriptionListDateField? dateField, SortingDirection? direction, ListSubscriptionComponentsFilter? filter, string? endDate, string? endDatetime, IncludeNotNull? pricePointIds, IReadOnlyList&lt;int&gt;? productFamilyIds, ListSubscriptionComponentsSort? sort, string? startDate, string? startDatetime, IReadOnlyList&lt;ListSubscriptionComponentsInclude&gt;? include, bool? inUse, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Lists a subscription's applied components.

## Archived Components

When requesting to list components for a given subscription, if the subscription contains **archived** components they will be listed in the server response.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.SubscriptionComponents.ListSubscriptionComponents(subscriptionId,
        dateField,
        direction,
        filter,
        endDate,
        endDatetime,
        pricePointIds,
        productFamilyIds,
        sort,
        startDate,
        startDatetime,
        include,
        inUse);
    // TODO: Handle 'response' of type IReadOnlyList<SubscriptionComponentResponse>
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
| <code>dateField</code> | <code>[SubscriptionListDateField?](Models/Enums/SubscriptionListDateField.cs)</code> | The type of filter you'd like to apply to your search. Use in query `date_field=updated_at`. |
| <code>direction</code> | <code>[SortingDirection?](Models/Enums/SortingDirection.cs)</code> | Controls the order in which results are returned.<br>Use in query `direction=asc`. |
| <code>filter</code> | <code>[ListSubscriptionComponentsFilter?](Models/ListSubscriptionComponentsFilter.cs)</code> | Filter to use for List Subscription Components operation |
| <code>endDate</code> | <code>string?</code> | The end date (format YYYY-MM-DD) with which to filter the date_field. Returns components with a timestamp up to and including 11:59:59PM in your site’s time zone on the date specified. |
| <code>endDatetime</code> | <code>string?</code> | The end date and time (format YYYY-MM-DD HH:MM:SS) with which to filter the date_field. Returns components with a timestamp at or before exact time provided in query. You can specify timezone in query - otherwise your site''s time zone will be used. If provided, this parameter will be used instead of end_date. |
| <code>pricePointIds</code> | <code>[IncludeNotNull?](Models/Enums/IncludeNotNull.cs)</code> | Allows fetching components allocation only if price point id is present. Use in query `price_point_ids=not_null`. |
| <code>productFamilyIds</code> | <code>IReadOnlyList&lt;int&gt;?</code> | Allows fetching components allocation with matching product family id based on provided ids. Use in query `product_family_ids=1,2,3`. |
| <code>sort</code> | <code>[ListSubscriptionComponentsSort?](Models/Enums/ListSubscriptionComponentsSort.cs)</code> | The attribute by which to sort. Use in query `sort=updated_at`. |
| <code>startDate</code> | <code>string?</code> | The start date (format YYYY-MM-DD) with which to filter the date_field. Returns components with a timestamp at or after midnight (12:00:00 AM) in your site’s time zone on the date specified. |
| <code>startDatetime</code> | <code>string?</code> | The start date and time (format YYYY-MM-DD HH:MM:SS) with which to filter the date_field. Returns components with a timestamp at or after exact time provided in query. You can specify timezone in query - otherwise your site''s time zone will be used. If provided, this parameter will be used instead of start_date. |
| <code>include</code> | <code>IReadOnlyList&lt;[ListSubscriptionComponentsInclude](Models/Enums/ListSubscriptionComponentsInclude.cs)&gt;?</code> | Allows including additional data in the response. Use in query `include=subscription,historic_usages`. |
| <code>inUse</code> | <code>bool?</code> | If in_use is set to true, it returns only components that are currently in use. However, if it's set to false or not provided, it returns all components connected with the subscription. |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>IReadOnlyList&lt;[SubscriptionComponentResponse](Models/SubscriptionComponentResponse.cs)&gt;</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RawError](Core/ErrorResponse/RawError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
