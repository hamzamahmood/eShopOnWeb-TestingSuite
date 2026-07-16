using System.Text.Json;

namespace Harness.Core;

/// <summary>Shared JSON conventions + a tiny profile-bundle loader. camelCase, case-insensitive, and
/// comment/trailing-comma tolerant so the hand-authored profile files stay pleasant to write.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static T Load<T>(string path)
    {
        var text = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(text, Options)
               ?? throw new InvalidOperationException($"empty/invalid JSON: {path}");
    }

    /// <summary>Resolve ${appUrl}/${mockUrl} tokens in a config value.</summary>
    public static string Resolve(string value, string appUrl, string mockUrl) =>
        value.Replace("${appUrl}", appUrl).Replace("${mockUrl}", mockUrl);
}
