using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aonik.Agents.Entities;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Framework;

/// <summary>
/// A single tenant-declared external REST call exposed as an <see cref="AIFunction"/> (Spec 033 §8.4).
/// The function's <see cref="JsonSchema"/> IS the tenant's declared parameter schema, so the model
/// sees a fixed parameter surface and cannot smuggle arbitrary fields. The invocation substitutes
/// <c>{placeholders}</c> in the URL template, sends remaining args as query (GET-like) or a JSON body
/// (write methods), injects auth decrypted server-side, and re-checks the egress allow-list on the
/// final URL. It is an <see cref="AIFunction"/> so the Spec 032 gate can wrap it when classified
/// mutating.
/// </summary>
internal sealed partial class DeclarativeHttpAIFunction : AIFunction
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);
    private const int MaxResponseChars = 8192;

    private readonly TenantHttpTool _tool;
    private readonly ITenantCredentialProtector _protector;
    private readonly ITenantEgressAllowList _egress;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly JsonElement _schema;

    public DeclarativeHttpAIFunction(
        TenantHttpTool tool,
        ITenantCredentialProtector protector,
        ITenantEgressAllowList egress,
        IHttpClientFactory? httpClientFactory)
    {
        _tool = tool;
        _protector = protector;
        _egress = egress;
        _httpClientFactory = httpClientFactory;
        _schema = ParseSchema(tool.ParameterSchemaJson);
    }

    public override string Name => _tool.Name;

    public override string Description => _tool.Description;

    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var url = PlaceholderRegex().Replace(_tool.UrlTemplate, match =>
        {
            var key = match.Groups[1].Value;
            consumed.Add(key);
            return Uri.EscapeDataString(StringifyArg(arguments, key));
        });

        var method = new HttpMethod(string.IsNullOrWhiteSpace(_tool.Method) ? "GET" : _tool.Method.ToUpperInvariant());
        var hasBody = method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch;

        // Remaining (non-placeholder) args go to the query string for read methods, or the JSON body.
        var remaining = arguments
            .Where(kvp => !consumed.Contains(kvp.Key))
            .ToList();

        if (!hasBody && remaining.Count > 0)
        {
            var query = string.Join("&", remaining.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(StringifyValue(kvp.Value))}"));
            url += (url.Contains('?', StringComparison.Ordinal) ? "&" : "?") + query;
        }

        // SSRF guard: re-check the FINAL, fully-substituted URL against the egress allow-list.
        if (!_egress.IsAllowed(url, out var reason))
        {
            return $"The '{_tool.Name}' tool was not called: {reason}";
        }

        using var request = new HttpRequestMessage(method, url);
        if (hasBody)
        {
            var body = JsonSerializer.Serialize(remaining.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        foreach (var (key, value) in TenantRemoteAuth.BuildHeaders(_tool.AuthKind, _tool.ProtectedAuthJson, _protector))
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        using var client = _httpClientFactory?.CreateClient("TenantHttpTool") ?? new HttpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CallTimeout);

        try
        {
            using var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            if (content.Length > MaxResponseChars)
            {
                content = content[..MaxResponseChars] + "…(truncated)";
            }

            return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{content}";
        }
        catch (Exception ex)
        {
            return $"The '{_tool.Name}' tool call failed: {ex.Message}";
        }
    }

    private static JsonElement ParseSchema(string? schemaJson)
    {
        if (!string.IsNullOrWhiteSpace(schemaJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(schemaJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    return doc.RootElement.Clone();
                }
            }
            catch
            {
                // fall through to the default empty-object schema
            }
        }

        using var fallback = JsonDocument.Parse("""{"type":"object","properties":{}}""");
        return fallback.RootElement.Clone();
    }

    private static string StringifyArg(AIFunctionArguments arguments, string key) =>
        arguments.TryGetValue(key, out var value) ? StringifyValue(value) : string.Empty;

    private static string StringifyValue(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } e => e.GetString() ?? string.Empty,
        JsonElement e => e.GetRawText(),
        _ => value.ToString() ?? string.Empty,
    };

    [GeneratedRegex(@"\{([A-Za-z0-9_]+)\}")]
    private static partial Regex PlaceholderRegex();
}
