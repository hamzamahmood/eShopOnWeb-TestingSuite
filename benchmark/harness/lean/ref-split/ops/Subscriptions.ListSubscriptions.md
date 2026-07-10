# Subscriptions.ListSubscriptions

_Controller: Subscriptions — from the Maxio SDK API reference._

<details>
<summary><code>Task&lt;IReadOnlyList&lt;SubscriptionResponse&gt;&gt; ListSubscriptions(SubscriptionStateFilter? state, int? product, int? productPricePointId, int? coupon, string? couponCode, SubscriptionDateField? dateField, DateTimeOffset? startDate, DateTimeOffset? endDate, DateTimeOffset? startDatetime, DateTimeOffset? endDatetime, IReadOnlyDictionary&lt;string, string&gt;? metadata, SortingDirection? direction, SubscriptionSort? sort, IReadOnlyList&lt;SubscriptionListInclude&gt;? include, int? page = 1, int? perPage = 20, CancellationToken ct = default);</code></summary>

<dl>
<dd>

### Description

<dl>
<dd>

Returns an array of subscriptions from a Site. Pay close attention to query string filters and pagination in order to control responses from the server.

## Search for a subscription

Use the query strings below to search for a subscription using the criteria available. The return value will be an array.

## Self-Service Page token

Self-Service Page token for the subscriptions is not returned by default. If this information is desired, the include[]=self_service_page_token parameter must be provided with the request.

</dd>
</dl>

### Usage

<dl>
<dd>

```csharp
try
{
    var response = await client.Subscriptions.ListSubscriptions(state,
        product,
        productPricePointId,
        coupon,
        couponCode,
        dateField,
        startDate,
        endDate,
        startDatetime,
        endDatetime,
        metadata,
        direction,
        sort,
        include);
    // TODO: Handle 'response' of type IReadOnlyList<SubscriptionResponse>
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
| <code>state</code> | <code>[SubscriptionStateFilter?](Models/Enums/SubscriptionStateFilter.cs)</code> | The current state of the subscription |
| <code>product</code> | <code>int?</code> | The product id of the subscription. (Note that the product handle cannot be used.) |
| <code>productPricePointId</code> | <code>int?</code> | The ID of the product price point. If supplied, product is required |
| <code>coupon</code> | <code>int?</code> | The numeric id of the coupon currently applied to the subscription. (This can be found in the URL when editing a coupon. Note that the coupon code cannot be used.) |
| <code>couponCode</code> | <code>string?</code> | The coupon code currently applied to the subscription |
| <code>dateField</code> | <code>[SubscriptionDateField?](Models/Enums/SubscriptionDateField.cs)</code> | The type of filter you'd like to apply to your search.  Allowed Values: , current_period_ends_at, current_period_starts_at, created_at, activated_at, canceled_at, expires_at, trial_started_at, trial_ended_at, updated_at |
| <code>startDate</code> | <code>DateTimeOffset?</code> | The start date (format YYYY-MM-DD) with which to filter the date_field. Returns subscriptions with a timestamp at or after midnight (12:00:00 AM) in your site’s time zone on the date specified. Use in query `start_date=2022-07-01`. |
| <code>endDate</code> | <code>DateTimeOffset?</code> | The end date (format YYYY-MM-DD) with which to filter the date_field. Returns subscriptions with a timestamp up to and including 11:59:59PM in your site’s time zone on the date specified. Use in query `end_date=2022-08-01`. |
| <code>startDatetime</code> | <code>DateTimeOffset?</code> | The start date and time (format YYYY-MM-DD HH:MM:SS) with which to filter the date_field. Returns subscriptions with a timestamp at or after exact time provided in query. You can specify timezone in query - otherwise your site's time zone will be used. If provided, this parameter will be used instead of start_date. Use in query `start_datetime=2022-07-01 09:00:05`. |
| <code>endDatetime</code> | <code>DateTimeOffset?</code> | The end date and time (format YYYY-MM-DD HH:MM:SS) with which to filter the date_field. Returns subscriptions with a timestamp at or before exact time provided in query. You can specify timezone in query - otherwise your site's time zone will be used. If provided, this parameter will be used instead of end_date. Use in query `end_datetime=2022-08-01 10:00:05`. |
| <code>metadata</code> | <code>IReadOnlyDictionary&lt;string, string&gt;?</code> | The value of the metadata field specified in the parameter. Use in query `metadata[my-field]=value&metadata[other-field]=another_value`. |
| <code>direction</code> | <code>[SortingDirection?](Models/Enums/SortingDirection.cs)</code> | Controls the order in which results are returned.<br>Use in query `direction=asc`. |
| <code>sort</code> | <code>[SubscriptionSort?](Models/Enums/SubscriptionSort.cs)</code> | The attribute by which to sort |
| <code>include</code> | <code>IReadOnlyList&lt;[SubscriptionListInclude](Models/Enums/SubscriptionListInclude.cs)&gt;?</code> | Allows including additional data in the response. Use in query: `include[]=self_service_page_token`. |
| <code>page</code> | <code>int?</code> | Result records are organized in pages. By default, the first page of results is displayed. The page parameter specifies a page number of results to fetch. You can start navigating through the pages to consume the results. You do this by passing in a page parameter. Retrieve the next page by adding ?page=2 to the query string. If there are no results to return, then an empty result set will be returned.<br>Use in query `page=1`.<br>**Default**: 1 |
| <code>perPage</code> | <code>int?</code> | This parameter indicates how many records to fetch in each request. Default value is 20. The maximum allowed values is 200; any per_page value over 200 will be changed to 200.<br>Use in query `per_page=200`.<br>**Default**: 20 |

</dd>
</dl>

### Response

<dl>
<dd>

**OnSuccess**: <code>IReadOnlyList&lt;[SubscriptionResponse](Models/SubscriptionResponse.cs)&gt;</code>

**OnError**: <code>[SdkException](Core/Exceptions/SdkException.cs)&lt;[RawError](Core/ErrorResponse/RawError.cs)&gt;</code>

</dd>
</dl>

</dd>
</dl>

</details>
