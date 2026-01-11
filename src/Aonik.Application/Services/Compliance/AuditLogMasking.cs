namespace Aonik.Application.Services.Compliance;

public static class AuditLogMasking
{
    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0 || atIndex >= trimmed.Length - 1)
        {
            return $"{trimmed[0]}***";
        }

        var domain = trimmed[(atIndex + 1)..];
        return $"{trimmed[0]}***@{domain}";
    }

    public static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var trimmed = phone.Trim();
        var visibleCount = Math.Min(4, trimmed.Length);
        var suffix = trimmed[^visibleCount..];
        return $"***{suffix}";
    }
}
