namespace Aonik.Platform.Services.Identity.AccessManagement;

/// <summary>
/// Pure helper that derives a category bucket from a permission key.
/// </summary>
internal static class AccessPermissionCategoryHelper
{
    public static string GetPermissionCategory(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return "General";
        }

        var parts = permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "General";
    }
}
