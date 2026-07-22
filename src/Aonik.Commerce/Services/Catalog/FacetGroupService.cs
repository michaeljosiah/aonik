using System.Text.RegularExpressions;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Facet-group authoring and reads (Spec 070 §5/§6/§11).</summary>
internal sealed partial class FacetGroupService : IFacetGroupService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public FacetGroupService(CommerceDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<IReadOnlyList<FacetGroupDto>> ListPublicAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var groups = await _dbContext.FacetGroups
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.IsActive)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Key)
            .ToListAsync(cancellationToken);

        return groups.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<FacetGroupDto>> ListAdminAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var groups = await _dbContext.FacetGroups
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Key)
            .ToListAsync(cancellationToken);

        return groups.Select(Map).ToList();
    }

    public async Task<FacetGroupDto> CreateAsync(CreateFacetGroupCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var key = NormalizeKey(command.Key);
        if (await _dbContext.FacetGroups.AnyAsync(g => g.TenantId == tenantId && g.Key == key, cancellationToken))
        {
            throw new StorefrontValidationException($"A facet group with key '{key}' already exists.");
        }

        var matchKind = NormalizeMatchKind(command.MatchKind);
        var sourcePath = ValidateSourcePath(matchKind, command.SourcePath);
        var optionsJson = BoundOptionsJson(command.OptionsJson);
        FacetDefinitions.ParseStrict(optionsJson, matchKind);

        var group = new FacetGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            Label = RequireLabel(command.Label),
            MatchKind = matchKind,
            SourcePath = sourcePath,
            OptionsJson = optionsJson,
            SortOrder = command.SortOrder,
            IsActive = true,
        };

        _dbContext.FacetGroups.Add(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(group);
    }

    public async Task<FacetGroupDto> UpdateAsync(
        Guid facetGroupId, UpdateFacetGroupCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var group = await _dbContext.FacetGroups
            .FirstOrDefaultAsync(g => g.Id == facetGroupId && g.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Facet group '{facetGroupId}' was not found.");

        group.Label = RequireLabel(command.Label);

        // Omitted means unchanged. Key and MatchKind are not editable at all: the key is the
        // stable request token (renaming it breaks every deep-linked storefront URL), and
        // changing how a live group matches is a retire-and-replace, not an edit.
        if (command.SourcePath is not null)
        {
            group.SourcePath = ValidateSourcePath(group.MatchKind, command.SourcePath);
        }

        if (command.OptionsJson is not null)
        {
            var optionsJson = BoundOptionsJson(command.OptionsJson);
            FacetDefinitions.ParseStrict(optionsJson, group.MatchKind);
            group.OptionsJson = optionsJson;
        }

        if (command.SortOrder is { } sortOrder)
        {
            group.SortOrder = sortOrder;
        }

        if (command.IsActive is { } isActive)
        {
            group.IsActive = isActive;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(group);
    }

    // ─── Internals ───────────────────────────────────────────────────────────

    /// <summary>SourcePath is required for Attribute/Range (it names the AttributesJson property
    /// read) and forbidden otherwise — a Category/Tag group carrying one is a confused definition
    /// that should fail at authoring, not surprise at matching (§11).</summary>
    private static string? ValidateSourcePath(string matchKind, string? sourcePath)
    {
        var trimmed = string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath.Trim();

        if (matchKind is FacetMatchKinds.Attribute or FacetMatchKinds.Range)
        {
            if (trimmed is null)
            {
                throw new StorefrontValidationException(
                    $"A {matchKind} facet group requires a sourcePath naming the attribute it reads.");
            }
            if (trimmed.Length > 128)
            {
                throw new StorefrontValidationException("A sourcePath is at most 128 characters.");
            }
            return trimmed;
        }

        if (trimmed is not null)
        {
            throw new StorefrontValidationException(
                $"A {matchKind} facet group must not carry a sourcePath; only Attribute and Range groups read one.");
        }

        return null;
    }

    private static string NormalizeKey(string? value)
    {
        var key = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (!KeyPattern().IsMatch(key))
        {
            throw new StorefrontValidationException(
                $"'{value}' is not a valid facet key; use 1-64 characters of a-z, 0-9 or '-'.");
        }
        return key;
    }

    private static string NormalizeMatchKind(string? value)
    {
        var kind = (value ?? string.Empty).Trim();
        kind = kind.Length == 0 ? string.Empty : char.ToUpperInvariant(kind[0]) + kind[1..].ToLowerInvariant();

        if (!FacetMatchKinds.IsKnown(kind))
        {
            throw new StorefrontValidationException(
                $"'{value}' is not a valid match kind; expected {FacetMatchKinds.Category}, {FacetMatchKinds.Tag}, " +
                $"{FacetMatchKinds.Attribute} or {FacetMatchKinds.Range}.");
        }
        return kind;
    }

    private static string RequireLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new StorefrontValidationException("A label is required.");
        }

        var trimmed = label.Trim();
        return trimmed.Length <= 128
            ? trimmed
            : throw new StorefrontValidationException("A label is at most 128 characters.");
    }

    /// <summary>4096 is the documented authoring bound (§11). The column itself is nvarchar(max) —
    /// SQL Server has no nvarchar(4096), so HasMaxLength is metadata there and this check is the
    /// only real enforcement. Without it every facet read and browse would parse an unbounded
    /// document forever after one oversized write.</summary>
    private static string BoundOptionsJson(string? optionsJson)
    {
        // Binding can put null into the non-nullable request parameter at runtime; measuring it
        // would escape as a 500 instead of the documented 400.
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            throw new StorefrontValidationException("optionsJson is required.");
        }

        return optionsJson.Length <= 4096
            ? optionsJson
            : throw new StorefrontValidationException("optionsJson is at most 4096 characters.");
    }

    private static FacetGroupDto Map(FacetGroup group) => new(
        group.Id, group.Key, group.Label, group.MatchKind, group.SourcePath, group.SortOrder, group.IsActive,
        FacetDefinitions.ParseLenient(group.OptionsJson)
            .Select(o => new FacetOptionDto(o.Value, o.Label, o.Min, o.Max))
            .ToList());

    [GeneratedRegex("^[a-z0-9-]{1,64}$")]
    private static partial Regex KeyPattern();
}
