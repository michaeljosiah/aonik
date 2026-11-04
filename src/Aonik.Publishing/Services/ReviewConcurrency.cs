using System.Security.Cryptography;
using System.Text.Json;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Services;

internal static class ReviewConcurrency
{
    public static string Digest<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    public static void RequireVersion(PublicationSubmission submission, string? expected)
    {
        byte[]? bytes = null;
        try { if (expected is { Length: >= 8 and <= 64 } && !expected.Any(char.IsWhiteSpace)) bytes = Convert.FromBase64String(expected); }
        catch (FormatException) { }
        if (bytes is null || !bytes.SequenceEqual(submission.RowVersion))
            throw new PublishingException(409, "review-version-changed", "Reload the current submission before deciding or replacing it.");
    }
}
