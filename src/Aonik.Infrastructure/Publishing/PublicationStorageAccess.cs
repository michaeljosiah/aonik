using System.Text.RegularExpressions;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Publishing;

internal static class PublicationStorageAccess
{
    internal static bool UsesLocalPermissions(string root, PublishingStorageOptions options, string share)
    {
        if (options.AccessControl == "Local") return true;
        if (options.AccessControl != "AzureFilesSmb" || !OperatingSystem.IsLinux()) throw Unavailable();
        VerifyAzureFilesMount(root, options.AzureFilesAccountName, share, File.ReadAllLines("/proc/self/mountinfo"));
        return false;
    }

    internal static void VerifyAzureFilesMount(string root, string? account, string share, IEnumerable<string> mounts)
    {
        if (account is null || !Regex.IsMatch(account, "^[a-z0-9]{3,24}$") || share is not ("intake" or "delivery")) throw Unavailable();
        var expectedSource = $"//{account}.file.core.windows.net/{share}";
        foreach (var line in mounts)
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var separator = Array.IndexOf(fields, "-");
            if (fields.Length < 10 || separator < 6 || separator + 3 >= fields.Length) continue;
            var mountedAt = Regex.Replace(fields[4], @"\\([0-7]{3})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 8)).ToString());
            if (mountedAt != root) continue;
            // Refuse a local directory, nested bind, different share or read-only mount.
            // Do not catch chmod failures and silently downgrade to share permissions.
            if (fields[3] != "/" || fields[separator + 1] != "cifs" || fields[separator + 2] != expectedSource
                || !fields[5].Split(',').Contains("rw") || !fields[separator + 3].Split(',').Contains("rw")) throw Unavailable();
            return;
        }
        throw Unavailable();
    }

    private static PublishingException Unavailable() => new(503, "publishing-storage-access-unqualified",
        "Restore the configured authenticated publishing share before writing objects.");
}
