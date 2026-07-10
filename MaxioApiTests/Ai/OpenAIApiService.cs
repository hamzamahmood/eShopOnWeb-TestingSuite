using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using Xunit.Sdk;

namespace MaxioApiTests.Ai;

/// <summary>AI payload verifier for response content validation.</summary>
public sealed class OpenAIApiService
{
    /// <summary>Process-wide instance, or <c>null</c> when AI comparison is disabled / unconfigured.</summary>
    public static readonly Lazy<OpenAIApiService?> Shared = new(CreateOrNull);

    private readonly IChatClient _chat;

    private OpenAIApiService(IChatClient chat) => _chat = chat;

    private static OpenAIApiService? CreateOrNull()
    {
        if (!TestSettings.AiComparisonEnabled || string.IsNullOrWhiteSpace(TestSettings.AiApiKey))
        {
            return null;
        }

        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(TestSettings.AiEndpoint))
        {
            options.Endpoint = new Uri(TestSettings.AiEndpoint);
        }

        var chat = new OpenAIClient(new ApiKeyCredential(TestSettings.AiApiKey), options)
            .GetChatClient(TestSettings.AiModel)
            .AsIChatClient();

        return new OpenAIApiService(chat);
    }

    /// <summary>Gets the shared verifier or fails the test if unavailable.</summary>
    public static OpenAIApiService Require(string intent) =>
        Shared.Value ?? throw new XunitException(
            $"[{intent}] AI payload verification is unavailable, so this test's response-content checks cannot " +
            "run. It is ON by default, so a missing verifier means no API key was resolved (set AI_API_KEY, or " +
            "OPENAI_API_KEY), or it was disabled via AI_COMPARISON_ENABLED=false. Provide a key (and optionally " +
            "AI_MODEL — default gpt-5.5) to run these tests.");

    /// <summary>Verifies payload against rules and returns verification report.</summary>
    public async Task<VerificationReport> VerifyAsync(
        string payloadJson, IReadOnlyList<string> rules, CancellationToken ct = default)
    {
        var response = await _chat.GetResponseAsync<VerificationReport>(
            BuildPrompt(payloadJson, rules),
            useJsonSchemaResponseFormat: TestSettings.AiUseJsonSchema,
            cancellationToken: ct);

        return response.Result;
    }

    private static string BuildPrompt(string payload, IReadOnlyList<string> rules) => $"""
        You are a precise API-response verifier. You receive a JSON payload and a numbered list of rules. For
        EACH rule, decide whether the payload satisfies THE RULE'S INTENT, matching on MEANING — never on exact
        key names, casing, nesting, units, or serialization format. Treat these as equivalent when the meaning
        matches: camelCase vs snake_case, renamed or duplicated keys, values nested at any depth, monetary
        amounts in cents vs dollars (or as a number vs a formatted string like "$10.00"), ids as strings vs
        numbers, booleans as true/false vs "true"/"false", and date/time values in ANY format or timezone
        (ISO-8601, RFC-1123, epoch seconds/millis, date-only, with or without offset) as long as they denote the
        same point in time or the same date. A field expressed in a different but semantically equivalent
        representation still SATISFIES a rule that asks for it.

        Be lenient about wording. If the payload reasonably fulfills what the rule asks, mark it passed. Only mark
        a rule failed when the payload genuinely CONTRADICTS it or OMITS the required information. Do not fail a
        rule over pedantic technicalities, synonyms, phrasing, or an inability to prove things the rule did not
        actually require (e.g. "a non-blank id" only needs a present, non-empty id value — not a proof of global
        uniqueness).

        Return exactly one result per rule, in order. For a FAILED rule, set `reason` to EXACTLY
        "<field>: missing" or "<field>: mismatched", where <field> is the SHORT name of the single response
        field/concept the rule is about (a few words, e.g. "subscription id", "subscription state", "plan
        price"); use "missing" when that field is absent from the payload, and "mismatched" when it is present
        but its value does not satisfy the rule. Do NOT put the rule text, any payload values, or any
        explanation in `reason` — only the field name and the "missing"/"mismatched" keyword. For a PASSED rule
        leave `reason` empty. Leave `rule` empty for every result. Set the top-level `passed` to true only if
        every rule passed.

        PAYLOAD:
        {payload}

        RULES:
        {string.Join("\n", rules.Select((r, i) => $"{i + 1}. {r}"))}
        """;
}
