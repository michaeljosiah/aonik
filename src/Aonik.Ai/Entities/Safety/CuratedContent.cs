using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities.Safety;

/// <summary>
/// A pre-vetted character a child may choose (Spec 096 §7).
///
/// <para>
/// Curation is the cheapest safety mechanism in the whole design: the child picks from a designed
/// cast rather than describing a person freely, which removes the real-person-likeness category
/// outright and most of the frightening-figure category with it. No classifier can match that,
/// because there is nothing left to classify.
/// </para>
/// </summary>
public class CuratedCharacter : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Stable id used in a request. Human-readable so an operator can reason about a log.</summary>
    public string CharacterKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Approved reference imagery, anchoring generation to something already reviewed.</summary>
    public string? ReferenceImageRef { get; set; }

    /// <summary>Lowest band this character is approved for; every older band inherits it.</summary>
    public string MinimumSafetyBand { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A story frame the child fills in (Spec 096 §7). Structure supplied, variables theirs — a far
/// narrower output distribution than open text, for a request that feels just as much like theirs.
/// </summary>
public class StoryTemplate : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The frame, e.g. "a journey to find the lost {thing}".</summary>
    public string Frame { get; set; } = string.Empty;

    public string MinimumSafetyBand { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
