using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services;

/// <summary>
/// Cross-module bridge that delegates <see cref="IUserMemorySaveProvider"/> calls
/// to the internal <see cref="IUserMemoryService.SetEntryAsync"/>.
/// </summary>
internal sealed class UserMemorySaveProvider : IUserMemorySaveProvider
{
    private readonly IUserMemoryService _memoryService;
    private readonly ILogger<UserMemorySaveProvider> _logger;

    public UserMemorySaveProvider(
        IUserMemoryService memoryService,
        ILogger<UserMemorySaveProvider> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    public async Task<UserMemorySaveResult> SaveAsync(
        UserMemorySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var entryType = ParseEntryType(request.EntryType);
        var source = ParseSource(request.Source);

        _logger.LogInformation(
            "Saving user memory: Key={Key}, EntryType={EntryType}, Source={Source}, Confidence={Confidence} for User {UserId}.",
            request.Key, entryType, source, request.Confidence, request.UserId);

        var existing = await _memoryService.GetCurrentEntriesAsync(
            request.UserId, entryType, cancellationToken: cancellationToken);
        var hadExisting = existing.Any(e => e.Key == request.Key);

        var result = await _memoryService.SetEntryAsync(
            new SetUserMemoryEntryRequest(
                UserId: request.UserId,
                EntryType: entryType,
                Key: request.Key,
                ValueJson: request.ValueJson,
                Confidence: request.Confidence,
                Source: source),
            cancellationToken);

        return new UserMemorySaveResult(
            EntryId: result.Id,
            Key: result.Key,
            WasSuperseded: hadExisting);
    }

    private static UserMemoryEntryType ParseEntryType(string entryType)
        => entryType.ToLowerInvariant() switch
        {
            "identity" => UserMemoryEntryType.Identity,
            "preference" => UserMemoryEntryType.Preference,
            "correction" => UserMemoryEntryType.Correction,
            "fact" => UserMemoryEntryType.Fact,
            _ => UserMemoryEntryType.Fact
        };

    private static UserMemorySource ParseSource(string source)
        => source.ToLowerInvariant() switch
        {
            "userstated" or "user_stated" => UserMemorySource.UserStated,
            "aiinferred" or "ai_inferred" => UserMemorySource.AiInferred,
            "systemderived" or "system_derived" => UserMemorySource.SystemDerived,
            _ => UserMemorySource.AiInferred
        };
}
