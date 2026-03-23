namespace Aonik.Platform.Contracts.Api.Bootstrap;

public record BootstrapInitializeRequest(
    string SetupSecret,
    string OwnerEmail,
    string? OwnerDisplayName = null);

public record BootstrapStatusResponse(
    string State,
    bool BootstrapEnabled,
    bool SetupSecretConfigured,
    int TenantCount,
    bool CanBootstrap,
    string? Message = null);
