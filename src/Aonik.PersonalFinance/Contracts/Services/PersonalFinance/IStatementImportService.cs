using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IStatementImportService
{
    Task<StatementImportResponse> UploadStatementAsync(
        UploadStatementImportRequest request,
        Stream fileStream,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatementImportResponse>> ListImportsAsync(
        CancellationToken cancellationToken = default);

    Task<StatementImportResponse?> GetImportAsync(
        Guid statementImportId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StatementImportRowResponse>> ListImportRowsAsync(
        Guid statementImportId,
        CancellationToken cancellationToken = default);

    Task<StatementImportApplyResponse> ApplyImportAsync(
        Guid statementImportId,
        CancellationToken cancellationToken = default);
}
