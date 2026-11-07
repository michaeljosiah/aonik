namespace Aonik.Publishing.Services;

public sealed class PublishingOptions
{
    public const string SectionName = "Publishing";

    // No channel is implicitly public or accepting submissions.
    public Dictionary<string, PublishingChannelOptions> Channels { get; set; } = [];
    public PublishingIntakeOptions Intake { get; set; } = new();
    public PackageVerifierOptions Verifier { get; set; } = new();
    public PublishingDeliveryOptions Delivery { get; set; } = new();
    public PublishingStorageOptions Storage { get; set; } = new();
}
