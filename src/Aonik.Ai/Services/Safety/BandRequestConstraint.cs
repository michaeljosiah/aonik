using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Spec 096 §7 — L1, enforced server-side.
///
/// <para>
/// A product UI that only <em>offers</em> curated choices is presentation, not a control: the
/// request arrives over HTTP and a modified client can put anything in it. This is the same lesson
/// <a href="../../../../../docs/specifications/089.workspaces.html">Spec 089 §8.1</a> learned about
/// read/write access and Spec 032 learned about tool approval, applied to the youngest band's
/// prompt box.
/// </para>
/// </summary>
internal sealed class BandRequestConstraint : IRequestConstraint
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public BandRequestConstraint(AiDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<ConstraintVerdict> EvaluateAsync(
        ConstrainedRequest request, CancellationToken cancellationToken = default)
    {
        // An unknown band resolves to the strictest constraints via BandConstraints.For, so a
        // missing band cannot widen what may be asked.
        var constraints = BandConstraints.For(request.SafetyBand);
        var hasFreeText = !string.IsNullOrWhiteSpace(request.FreeText);

        if (hasFreeText && !constraints.AllowsFreeText)
        {
            // The under-6 rule, and the single most valuable line in this file. Free-text prompting
            // by young children is the riskiest feature in the product, and the youngest band is
            // better served by a curated experience than an open box.
            return ConstraintVerdict.Refuse(
                $"Band '{request.SafetyBand}' may not submit free text.");
        }

        if (hasFreeText && request.FreeText!.Length > constraints.MaxFreeTextLength)
        {
            // Length is a crude control and a real one: a long prompt is where instructions get
            // buried, and a bounded one is far cheaper to classify well.
            return ConstraintVerdict.Refuse(
                $"Free text exceeds {constraints.MaxFreeTextLength} characters for band '{request.SafetyBand}'.");
        }

        if (constraints.RequiresTemplate && string.IsNullOrWhiteSpace(request.TemplateId))
        {
            return ConstraintVerdict.Refuse(
                $"Band '{request.SafetyBand}' requires a story template.");
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (!string.IsNullOrWhiteSpace(request.TemplateId)
            && !await IsTemplateApprovedAsync(tenantId, request.TemplateId, request.SafetyBand, cancellationToken))
        {
            return ConstraintVerdict.Refuse(
                $"Template '{request.TemplateId}' is not approved for band '{request.SafetyBand}'.");
        }

        var characters = request.CharacterIds ?? [];

        if (constraints.RequiresCuratedCharacters && characters.Count == 0)
        {
            return ConstraintVerdict.Refuse(
                $"Band '{request.SafetyBand}' requires curated characters.");
        }

        foreach (var characterKey in characters)
        {
            // Checked individually rather than as a set: a request naming nine approved characters
            // and one unapproved one is an unapproved request, and a count check would pass it.
            if (!await IsCharacterApprovedAsync(tenantId, characterKey, request.SafetyBand, cancellationToken))
            {
                return ConstraintVerdict.Refuse(
                    $"Character '{characterKey}' is not approved for band '{request.SafetyBand}'.");
            }
        }

        return ConstraintVerdict.Allow;
    }

    private Task<bool> IsTemplateApprovedAsync(
        Guid tenantId, string templateKey, string band, CancellationToken cancellationToken)
        => _dbContext.StoryTemplates
            .AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId
                && t.TemplateKey == templateKey
                && t.IsActive
                && ApprovedBands(band).Contains(t.MinimumSafetyBand), cancellationToken);

    private Task<bool> IsCharacterApprovedAsync(
        Guid tenantId, string characterKey, string band, CancellationToken cancellationToken)
        => _dbContext.CuratedCharacters
            .AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId
                && c.CharacterKey == characterKey
                && c.IsActive
                && ApprovedBands(band).Contains(c.MinimumSafetyBand), cancellationToken);

    /// <summary>
    /// Bands whose approved content this band may use: its own and every younger one.
    ///
    /// <para>
    /// Approval flows upward only. Something approved for a six-year-old is fine for a twelve-year-old;
    /// the reverse is the mistake, and expressing it as a list rather than an ordering comparison is
    /// what stops a future band being inserted in the middle and silently widening access.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> ApprovedBands(string band) => band switch
    {
        SafetyBandNames.Under6 => [SafetyBandNames.Under6],
        SafetyBandNames.Age6To9 => [SafetyBandNames.Under6, SafetyBandNames.Age6To9],
        SafetyBandNames.Age10To12 =>
            [SafetyBandNames.Under6, SafetyBandNames.Age6To9, SafetyBandNames.Age10To12],
        SafetyBandNames.Age13ToMajority =>
            [SafetyBandNames.Under6, SafetyBandNames.Age6To9, SafetyBandNames.Age10To12, SafetyBandNames.Age13ToMajority],
        // Unknown band: only the youngest-approved content, matching the strict constraints above.
        _ => [SafetyBandNames.Under6],
    };
}
