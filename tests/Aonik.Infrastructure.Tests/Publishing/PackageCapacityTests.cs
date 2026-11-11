using System.Text.Json;

using Aonik.Infrastructure.Messaging.Outbox;
using Aonik.Publishing.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Tests.Publishing;

public class PackageCapacityTests
{
    [Fact]
    public void DefaultPolicy_Should_PreserveExistingSerializedApprovalIdentity()
    {
        JsonSerializer.Serialize(new PackageVerifierOptions()).Should().NotContain("CapacityProfile");
    }

    [Theory]
    [InlineData(null, 50, 300)]
    [InlineData("large-v1", 1, 1260)]
    public void Outbox_Should_NotLeaseABatchBehindLongVerification(string? profile, int batch, int lease)
    {
        var services = new ServiceCollection();
        services.Configure<PublishingOptions>(p => p.Verifier.CapacityProfile = profile);
        services.AddOutboxProcessing(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OutboxOptions>>().Value;
        options.BatchSize.Should().Be(batch);
        options.ClaimLeaseSeconds.Should().Be(lease);
    }
}
