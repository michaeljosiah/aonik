using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Models.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services.Compliance;

internal class ComplianceService : IComplianceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IOrderExistenceChecker _orderExistenceChecker;
    private readonly ILogger<ComplianceService> _logger;

    public ComplianceService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        IAuditLogWriter auditLogWriter,
        IOrderExistenceChecker orderExistenceChecker,
        ILogger<ComplianceService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
        _orderExistenceChecker = orderExistenceChecker;
        _logger = logger;
    }

    public async Task<ScreeningResult> ScreenPartyAsync(
        Guid partyId,
        string checkType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkType))
        {
            throw new ArgumentException("Check type is required.", nameof(checkType));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var partyExists = await _dbContext.Parties
            .AsNoTracking()
            .AnyAsync(party => party.Id == partyId && party.TenantId == tenantId, cancellationToken);

        if (!partyExists)
        {
            throw new InvalidOperationException($"Party {partyId} not found.");
        }

        _logger.LogWarning("Compliance screening is a stub — always returns Passed for Party {PartyId}", partyId);

        var now = _clock.UtcNow;
        var screening = new ScreeningCheck
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyId = partyId,
            CheckType = checkType.Trim(),
            ResultStatus = "Passed",
            ResultJson = JsonSerializer.Serialize(new { status = "Passed" }, JsonOptions),
            Decision = "Approved",
            DecidedAt = now
        };

        _dbContext.ScreeningChecks.Add(screening);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.PartyScreened,
            "ScreeningCheck",
            screening.Id,
            tenantId,
            actorId: null,
            correlationId: null,
            detailsJson: screening.ResultJson,
            cancellationToken: cancellationToken);

        return new ScreeningResult(
            screening.Id,
            screening.PartyId,
            screening.CheckType,
            screening.ResultStatus,
            screening.Decision,
            screening.DecidedAt);
    }

    public async Task<ComplianceCaseResponse> CreateOrderReviewCaseAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var orderExists = await _orderExistenceChecker.OrderExistsAsync(orderId, cancellationToken);

        if (!orderExists)
        {
            throw new InvalidOperationException($"Order {orderId} not found.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var complianceCase = new ComplianceCase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CaseType = "OrderReview",
            LinkedOrderId = orderId,
            Status = "Pending",
            DetailsJson = JsonSerializer.Serialize(new { orderId }, JsonOptions)
        };

        _dbContext.ComplianceCases.Add(complianceCase);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            AuditEventNames.ComplianceCaseCreated,
            "ComplianceCase",
            complianceCase.Id,
            tenantId,
            actorId: null,
            correlationId: null,
            detailsJson: complianceCase.DetailsJson,
            cancellationToken: cancellationToken);

        return new ComplianceCaseResponse(
            complianceCase.Id,
            complianceCase.CaseType,
            complianceCase.Status,
            complianceCase.LinkedOrderId,
            complianceCase.LinkedPartyId);
    }

    public Task<bool> RequiresComplianceReviewAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        _ = orderId;
        _ = cancellationToken;
        return Task.FromResult(false);
    }
}
