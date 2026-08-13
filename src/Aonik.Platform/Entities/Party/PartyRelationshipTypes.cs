using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Entities.Party;

public static class PartyRelationshipTypes
{
    public const string Self = "Self";
    public const string Mother = "Mother";
    public const string Father = "Father";
    public const string Spouse = "Spouse";
    public const string Sibling = "Sibling";
    public const string Child = "Child";
    public const string Friend = "Friend";
    public const string Business = "Business";
    public const string Other = "Other";

    /// <summary>
    /// Neutral, non-kin payee edge. Aliased to <see cref="PartyRelationshipTypeCodes.Recipient"/> so
    /// the Finance module can create a customer→beneficiary edge via <c>IPartyService</c> using the
    /// exact code this validation set accepts. Kinship codes above stay for relatives.
    /// </summary>
    public const string Recipient = PartyRelationshipTypeCodes.Recipient;

    /// <summary>
    /// Legal authority to act for a child (Spec 095 §7). Deliberately NOT a synonym for
    /// <see cref="Mother"/> or <see cref="Father"/>: those describe family structure, this describes
    /// authority, and they diverge in both directions — foster and kinship carers, step-parents with
    /// a responsibility agreement, and biological parents who do not hold it.
    ///
    /// <para>
    /// <strong>Privileged.</strong> Every other code in this set merely describes, so
    /// <c>PartyService.CreateRelationshipAsync</c> validates set membership and nothing else. That is
    /// unsafe for a code that <em>authorises</em>: the generic path is fed caller-supplied codes by
    /// ordinary finance workflows, so an order request could otherwise mint an edge that
    /// <c>IGuardianshipReader</c> later trusts for access to a child's data. See
    /// <see cref="Privileged"/>.
    /// </para>
    /// </summary>
    public const string Guardian = "Guardian";

    public static readonly IReadOnlyList<PartyRelationshipTypeDefinition> All =
        new List<PartyRelationshipTypeDefinition>
        {
            new(Self, Self, 1),
            new(Mother, Mother, 2),
            new(Father, Father, 3),
            new(Spouse, Spouse, 4),
            new(Sibling, Sibling, 5),
            new(Child, Child, 6),
            new(Friend, Friend, 7),
            new(Business, Business, 8),
            new(Other, Other, 9),
            new(Recipient, Recipient, 10),
            new(Guardian, Guardian, 11, IsPrivileged: true)
        };

    public static readonly IReadOnlySet<string> Codes =
        new HashSet<string>(All.Select(type => type.Code), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Codes that grant authority rather than describe a relationship. These are rejected by the
    /// generic <c>IPartyService</c> create and update paths and may only be written by the service
    /// that owns their verification — for <see cref="Guardian"/>, that is the consent service.
    ///
    /// <para>
    /// Marked here rather than remembered, so a future authority-carrying code inherits the refusal
    /// instead of re-learning why it is needed.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> Privileged =
        new HashSet<string>(
            All.Where(type => type.IsPrivileged).Select(type => type.Code),
            StringComparer.OrdinalIgnoreCase);

    public static bool IsPrivileged(string? code)
        => !string.IsNullOrWhiteSpace(code) && Privileged.Contains(code);
}

public record PartyRelationshipTypeDefinition(
    string Code,
    string DisplayName,
    int SortOrder,
    bool IsPrivileged = false);
