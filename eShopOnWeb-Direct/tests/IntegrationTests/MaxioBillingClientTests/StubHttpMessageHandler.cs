#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.IntegrationTests.MaxioBillingClientTests;

/// <summary>
/// Test double for the Maxio HTTP endpoint. Routes each request (by "METHOD path") to a canned
/// response and records every request so tests can assert on the URL, method and body — letting us
/// exercise <c>MaxioBillingClient</c> end-to-end over HTTP without a live Maxio site or API key.
/// </summary>
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Json)> _routes = new(StringComparer.OrdinalIgnoreCase);

    public List<RecordedRequest> Requests { get; } = new();

    public StubHttpMessageHandler Map(HttpMethod method, string pathAndQuery, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[$"{method} {pathAndQuery}"] = (status, json);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var pathAndQuery = request.RequestUri!.PathAndQuery.TrimStart('/');
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.Method, pathAndQuery, body));

        var key = $"{request.Method} {pathAndQuery}";
        if (_routes.TryGetValue(key, out var route))
        {
            return new HttpResponseMessage(route.Status)
            {
                Content = new StringContent(route.Json, System.Text.Encoding.UTF8, "application/json")
            };
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("\"not found\"", System.Text.Encoding.UTF8, "application/json")
        };
    }

    public record RecordedRequest(HttpMethod Method, string PathAndQuery, string? Body);
}
