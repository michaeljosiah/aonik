using System.Text.Json.Serialization;

namespace Aonik.Publishing.Services;

public sealed class PackageVerifierOptions
{
    // Omit the default to preserve the exact serialized policy of existing review approvals.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CapacityProfile { get; set; }
    public string Provider { get; set; } = "Docker";
    public string SessionPoolResourceId { get; set; } = string.Empty;
    public string DockerExecutable { get; set; } = "docker";
    public string Image { get; set; } = string.Empty;
    public string ReaderCommit { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 45;
    public int MemoryMiB { get; set; } = 512;
    public int ScratchMiB { get; set; } = 512;
    public int MaxAssets { get; set; } = 1024;
    public long MaxAssetBytes { get; set; } = 256L * 1024 * 1024;
    public int MaxDocumentBytes { get; set; } = 4 * 1024 * 1024;
}
