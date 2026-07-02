using System.Collections.Frozen;

namespace MaxioMockServer;

/// <summary>
/// Holds the canned response payloads (loaded once at startup) and the set of
/// "known" identifiers that map to a successful response. Anything not in these
/// sets is treated as not-found (404), matching the real Maxio API behavior.
/// </summary>
public sealed class MockStore
{
    /// <summary>Raw JSON for <c>GET /product_families/{id}/products.json</c>.</summary>
    public string ProductsJson { get; }

    /// <summary>Raw JSON for <c>GET /customers/lookup.json</c>.</summary>
    public string CustomerJson { get; }

    /// <summary>Raw JSON for <c>GET /customers/{id}/subscriptions.json</c>.</summary>
    public string SubscriptionsJson { get; }

    /// <summary>Product family ids/handles that return the canned product list.</summary>
    public FrozenSet<string> KnownProductFamilyIds { get; } =
        new[] { "527890", "handle:acme-projects" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Customer <c>reference</c> values that resolve to the canned customer.</summary>
    public FrozenSet<string> KnownCustomerReferences { get; } =
        new[] { "cust_12345" }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Customer ids that return the canned subscription list.</summary>
    public FrozenSet<int> KnownCustomerIds { get; } =
        new[] { 98765 }.ToFrozenSet();

    private MockStore(string productsJson, string customerJson, string subscriptionsJson)
    {
        ProductsJson = productsJson;
        CustomerJson = customerJson;
        SubscriptionsJson = subscriptionsJson;
    }

    /// <summary>
    /// Loads the canned JSON files from the <c>MockData</c> folder that sits next
    /// to the running assembly (copied there by the build).
    /// </summary>
    public static MockStore Load(string contentRootPath)
    {
        var dir = Path.Combine(contentRootPath, "MockData");

        string Read(string fileName)
        {
            var path = Path.Combine(dir, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Required mock data file was not found: {path}", path);
            }
            return File.ReadAllText(path);
        }

        return new MockStore(
            productsJson: Read("products.json"),
            customerJson: Read("customer.json"),
            subscriptionsJson: Read("subscriptions.json"));
    }
}
