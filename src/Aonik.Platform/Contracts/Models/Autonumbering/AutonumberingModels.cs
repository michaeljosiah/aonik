using Aonik.Platform.Entities.Autonumbering;

namespace Aonik.Platform.Contracts.Models.Autonumbering;

public record AutonumberProfileSnapshot(
    Guid Id,
    Guid TenantId,
    string EntityType,
    string PrefixTemplate,
    string SuffixTemplate,
    AutonumberStrategy Strategy,
    AutonumberResetPolicy ResetPolicy,
    int PaddingLength,
    long MinValue,
    long MaxValue,
    long LastIssuedValue,
    DateTime? LastIssuedAt,
    bool IsActive);

public record AutonumberProfileUpsert(
    string EntityType,
    string? PrefixTemplate,
    string? SuffixTemplate,
    AutonumberStrategy Strategy,
    AutonumberResetPolicy ResetPolicy,
    int PaddingLength,
    long MinValue,
    long MaxValue,
    bool IsActive);

public record AutonumberGenerateRequest(
    string EntityType,
    Guid? TenantId = null);

public record AutonumberGenerateResult(
    Guid ProfileId,
    long SequenceValue,
    string Reference);
