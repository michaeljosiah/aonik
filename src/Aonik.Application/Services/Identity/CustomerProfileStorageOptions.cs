namespace Aonik.Application.Services.Identity;

public class CustomerProfileStorageOptions
{
    public string BlobRootPath { get; set; } = "profiles";
    public string? PublicBaseUrl { get; set; }
    public string LocalStoragePath { get; set; } = "App_Data/Profiles";
}
