namespace Aonik.Platform.Services.Identity;

internal class BootstrapOptions
{
    public bool Enabled { get; set; }
    public string? SetupSecret { get; set; }
    public string TenantName { get; set; } = "Aonik Dev Tenant";
    public string Environment { get; set; } = "Development";
    public string DefaultCurrency { get; set; } = "USD";
    public string[] SupportedCountries { get; set; } = ["US"];
}
