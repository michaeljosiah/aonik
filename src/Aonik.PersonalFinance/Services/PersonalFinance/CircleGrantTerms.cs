using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// The PersonalFinance reading of a share grant's <c>TermsJson</c> (Spec 086 §7).
/// </summary>
/// <remarks>
/// <c>NoAmounts</c> is a finance redaction rule that ended up as a column on what is now a platform
/// entity. Spec 086 moves it into an opaque terms blob the platform stores and never reads, which is
/// what keeps one domain's redaction vocabulary off a shared table.
///
/// This type exists so the P3 backfill and the P3 dual-writers cannot disagree about the shape.
/// A backfill that wrote <c>{"noAmounts":true}</c> against a writer emitting <c>{"NoAmounts":true}</c>
/// would produce two silently different corpora, and the divergence would only surface at the P5
/// reader cutover — by which point half the grants would have stopped redacting.
/// </remarks>
internal sealed record CircleGrantTerms
{
    [JsonPropertyName("noAmounts")]
    public bool NoAmounts { get; init; }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string Serialize(bool noAmounts)
        => JsonSerializer.Serialize(new CircleGrantTerms { NoAmounts = noAmounts }, SerializerOptions);

    /// <summary>
    /// Reads the redaction term, falling back to the column.
    /// </summary>
    /// <remarks>
    /// The <c>NoAmounts</c> column is retained and dual-written through the transition (§10.2), so a
    /// grant predating the backfill — or one written by a rolled-back deployment — still answers
    /// correctly. Unreadable terms fall back too: terms are opaque to the platform and could hold
    /// anything a future module writes, and a parse failure must not make a grant stop redacting.
    /// </remarks>
    public static bool ReadNoAmounts(string? termsJson, bool columnValue)
    {
        if (string.IsNullOrWhiteSpace(termsJson))
        {
            return columnValue;
        }

        try
        {
            return JsonSerializer.Deserialize<CircleGrantTerms>(termsJson, SerializerOptions)?.NoAmounts ?? columnValue;
        }
        catch (JsonException)
        {
            return columnValue;
        }
    }
}
