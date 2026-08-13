using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Party;

/// <summary>
/// A published terms version and the external companies it names (Spec 095 §9, Spec 096 §16.1).
///
/// <para>
/// The provider list is part of the <em>terms</em>, not an implementation detail, because the consent
/// text has to name who a child's words are sent to. That makes adding a provider a terms change
/// rather than a configuration edit — with the publication-time revocation that implies.
/// </para>
///
/// <para>
/// The operational consequence is real and accepted: a vendor added during an incident cannot be
/// used until terms are published and families re-consent, which is exactly when you least want the
/// constraint. The answer is to name the <strong>full candidate set</strong> up front rather than
/// the current provider — slightly longer terms, and the right trade, because a family who agreed to
/// a named set is being told the truth and one who agreed to a single provider and got a different
/// one is not.
/// </para>
/// </summary>
public class ConsentTermsVersion : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Matches <c>ConsentGrant.TermsVersion</c>.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Comma-separated provider names this version names. The full candidate set, including failover.</summary>
    public string NamedProviders { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; }

    public bool IsCurrent { get; set; }
}
