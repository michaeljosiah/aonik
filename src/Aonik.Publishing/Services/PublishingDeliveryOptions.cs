namespace Aonik.Publishing.Services;

public sealed class PublishingDeliveryOptions
{
    public string PublicRoot { get; set; } = "";
    public int LeaseMinutes { get; set; } = 10;
    public int MaxAttempts { get; set; } = 6;
    public int MaxPictures { get; set; } = 128;
}

public sealed class LegacyWorldOwnership
{
    public Guid CreatorPartyId { get; set; }
    public string EvidenceReference { get; set; } = "";
    public Dictionary<string, List<PublicationChapterHead>> ProductionChapters { get; set; } = new();
}

public sealed record PublicationChapterHead(string Id, int Number);
