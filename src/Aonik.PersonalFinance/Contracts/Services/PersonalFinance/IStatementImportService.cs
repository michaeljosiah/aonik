using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

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
