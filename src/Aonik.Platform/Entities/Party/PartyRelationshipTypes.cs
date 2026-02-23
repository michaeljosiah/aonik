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
            new(Other, Other, 9)
        };

    public static readonly IReadOnlySet<string> Codes =
        new HashSet<string>(All.Select(type => type.Code), StringComparer.OrdinalIgnoreCase);
}

public record PartyRelationshipTypeDefinition(string Code, string DisplayName, int SortOrder);
