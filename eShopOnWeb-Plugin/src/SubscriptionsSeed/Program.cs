// UC0 operator tool - verifies (and, only if missing, creates) the product family, plans, and metered
// component the subscription integration expects. Not wired into Web/PublicApi; run manually, once per
// sandbox. Never touches any family/product/component other than the ones this integration owns.
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.Authentication.Basic;
using MaxioAdvancedBilling.Core.ErrorResponse;
using MaxioAdvancedBilling.Core.Exceptions;
using MaxioAdvancedBilling.Models;
using MaxioAdvancedBilling.Models.Enums;
using MaxioAdvancedBilling.Servers;
using Microsoft.eShopWeb.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var settings = configuration.GetSection("Maxio").Get<MaxioSettings>();
if (settings is null || string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Subdomain))
{
    Console.Error.WriteLine("Missing Maxio configuration. Set Maxio:ApiKey and Maxio:Subdomain via user-secrets or environment variables (see the integration plan §6).");
    return 1;
}

var options = new MaxioAdvancedBillingClientOptions
{
    Environment = string.Equals(settings.Environment, "EU", StringComparison.OrdinalIgnoreCase) ? ServerEnvironment.Eu : ServerEnvironment.Us,
    BasicAuth = new BasicAuthCredentials { Username = settings.ApiKey, Password = "x" }
};
options.Server.Production.Us.Site = settings.Subdomain;
options.Server.Production.Eu.Site = settings.Subdomain;

using var httpClient = new HttpClient();
var client = new MaxioAdvancedBillingClient(httpClient, options);

var ok = true;

Console.WriteLine($"Verifying Maxio sandbox '{settings.Subdomain}' for the eShopOnWeb subscriptions integration...");
Console.WriteLine();

// 1. Product family
var families = await client.ProductFamilies.ListProductFamilies(null, null, null, null, null);
var family = families.Select(f => f.ProductFamily).FirstOrDefault(f => f?.Handle == settings.ProductFamilyHandle);

if (family is null)
{
    Console.WriteLine($"Family '{settings.ProductFamilyHandle}' not found - creating it.");
    var created = await client.ProductFamilies.CreateProductFamily(new CreateProductFamilyRequest
    {
        ProductFamily = new CreateProductFamily
        {
            Name = "eShopSubscribe",
            Handle = settings.ProductFamilyHandle,
            Description = "eShopOnWeb subscription plans and metered usage (Maxio integration)."
        }
    });
    family = created.ProductFamily;
    Console.WriteLine($"  Created: id={family!.Id}");
}
else
{
    Console.WriteLine($"[OK] Family '{family.Handle}' exists (id={family.Id}).");
}

var familyId = family!.Id!.Value;
if ((long)familyId != settings.ProductFamilyId)
{
    Console.WriteLine($"[WARN] Configured Maxio:ProductFamilyId ({settings.ProductFamilyId}) does not match the live id ({familyId}). Update configuration.");
    ok = false;
}

// 2. Products
async Task<bool> EnsureProductAsync(string handle, string name, long priceInCents, long configuredId)
{
    var products = await client.ProductFamilies.ListProductsForProductFamily(familyId.ToString(),
        dateField: null, filter: null, startDate: null, endDate: null, startDatetime: null, endDatetime: null,
        includeArchived: false, include: null);
    var existing = products.Select(p => p.Product).FirstOrDefault(p => p?.Handle == handle);

    if (existing is null)
    {
        Console.WriteLine($"Product '{handle}' not found - creating it.");
        var created = await client.Products.CreateProduct(familyId.ToString(), new CreateOrUpdateProductRequest
        {
            Product = new CreateOrUpdateProduct
            {
                Name = name,
                Handle = handle,
                Description = name,
                PriceInCents = priceInCents,
                Interval = 1,
                IntervalUnit = IntervalUnit.Month,
                RequireCreditCard = false
            }
        });
        Console.WriteLine($"  Created: id={created.Product.Id} price={created.Product.PriceInCents}c");
        return (long)created.Product.Id!.Value == configuredId || configuredId == 0;
    }

    Console.WriteLine($"[OK] Product '{handle}' exists (id={existing.Id}, price={existing.PriceInCents}c, requiresCard={existing.RequireCreditCard}).");
    var matches = true;
    if (existing.PriceInCents != priceInCents)
    {
        Console.WriteLine($"  [WARN] Expected price {priceInCents}c, found {existing.PriceInCents}c.");
        matches = false;
    }
    if (existing.RequireCreditCard == true)
    {
        Console.WriteLine("  [WARN] Expected requiresCard=false (demo subscribes without a payment method).");
        matches = false;
    }
    if ((long)existing.Id!.Value != configuredId)
    {
        Console.WriteLine($"  [WARN] Configured id ({configuredId}) does not match the live id ({existing.Id}). Update configuration.");
        matches = false;
    }
    return matches;
}

ok &= await EnsureProductAsync(settings.DefaultProductHandle, "Pro Plan", 29900, settings.DefaultProductId);
ok &= await EnsureProductAsync(settings.AlternateProductHandle, "Basic Plan", 2900, settings.AlternateProductId);

// 3. Metered component
ComponentResponse? existingComponentResponse = null;
try
{
    existingComponentResponse = await client.Components.FindComponent(settings.MeteredComponentHandle);
}
catch (SdkException<RawError>)
{
    // not found - fall through to create
}

if (existingComponentResponse is null)
{
    Console.WriteLine($"Component '{settings.MeteredComponentHandle}' not found - creating it.");
    var created = await client.Components.CreateMeteredComponent(familyId.ToString(), new CreateMeteredComponent
    {
        MeteredComponent = new MeteredComponent
        {
            Name = "API Calls",
            UnitName = "call",
            Handle = settings.MeteredComponentHandle,
            PricingScheme = PricingScheme.PerUnit,
            UnitPrice = 0.01m,
            Taxable = false
        }
    });
    Console.WriteLine($"  Created: id={created.Component.Id}");
}
else
{
    var component = existingComponentResponse.Component!;
    Console.WriteLine($"[OK] Component '{component.Handle}' exists (id={component.Id}, kind={component.Kind}).");
    if (component.Kind != ComponentKind.MeteredComponent)
    {
        Console.WriteLine($"  [WARN] Expected kind 'metered_component', found '{component.Kind}'. A component's kind cannot be changed in place - archive and recreate it.");
        ok = false;
    }
    if (component.ProductFamilyId != (double)familyId)
    {
        Console.WriteLine("  [WARN] Component does not belong to the configured product family.");
        ok = false;
    }
    if ((long)component.Id!.Value != settings.MeteredComponentId)
    {
        Console.WriteLine($"  [WARN] Configured Maxio:MeteredComponentId ({settings.MeteredComponentId}) does not match the live id ({component.Id}). Update configuration.");
        ok = false;
    }
}

Console.WriteLine();
Console.WriteLine(ok ? "UC0 seed verification PASSED - configuration matches the live sandbox." : "UC0 seed verification FOUND MISMATCHES - see [WARN] lines above and update configuration.");
return ok ? 0 : 1;
