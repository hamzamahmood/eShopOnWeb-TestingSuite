using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Microsoft.eShopWeb.PublicApi.SubscriptionEndpoints;

/// <summary>
/// Minimal API has no MVC-style automatic model validation, so every request DTO's DataAnnotations are
/// validated explicitly here before any domain call (quality-gate G1/G3).
/// </summary>
internal static class RequestValidation
{
    public static bool TryValidate(object request, out IDictionary<string, string[]> errors)
    {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, context, results, validateAllProperties: true);

        errors = results
            .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty).Select(member => (Member: member, r.ErrorMessage)))
            .GroupBy(x => x.Member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage ?? "Invalid value").ToArray());

        return isValid;
    }
}
