using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;

using Aonik.Application.Options;
using Aonik.Infrastructure.Integrations;
using Aonik.Infrastructure.Storage;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Infrastructure.Tests.Storage;

public sealed class ConnectorContentRetentionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aonik-connector-storage-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Purge_Should_DeleteExpiredConnectorBytes_AndPreserveCurrentAndUnrelatedFiles()
    {
        Guid tenantId = Guid.NewGuid();
        Guid expiredConnection = Guid.NewGuid();
        var settings = new BlobStorageOptions { LocalBasePath = Path.Combine(_root, "App_Data") };
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.ContentRootPath).Returns(_root);
        environment.SetupGet(value => value.WebRootPath).Returns(Path.Combine(_root, "wwwroot"));
        var options = Options.Create(settings);
        var clock = new TestClock { UtcNow = DateTime.UtcNow.AddDays(-8) };
        var store = new ConnectorContentStore(new BlobStorageFactoryService(options), options, environment.Object,
            new EphemeralDataProtectionProvider(), new TestTenantProvider(tenantId), clock);
        ConnectorStoredContent expired = await store.SaveAsync(expiredConnection, "message-1", null, [1], "expired.txt", "text/plain");
        string expiredDirectory = Path.Combine(settings.LocalBasePath, settings.ConnectorContent.Path, "tenants", tenantId.ToString("N"), expiredConnection.ToString("N"));
        string expiredPath = Directory.EnumerateFiles(expiredDirectory).Single();
        File.SetLastWriteTimeUtc(expiredPath, clock.UtcNow);
        clock.UtcNow = clock.UtcNow.AddDays(8);
        ConnectorStoredContent current = await store.SaveAsync(Guid.NewGuid(), "message-2", null, [2], "current.txt", "text/plain");
        string otherDirectory = Path.Combine(settings.LocalBasePath, settings.PrivateDocuments.Path, "tenants", tenantId.ToString("N"), expiredConnection.ToString("N"));
        Directory.CreateDirectory(otherDirectory);
        string unrelated = Path.Combine(otherDirectory, Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(unrelated, "Retained document");
        File.SetLastWriteTimeUtc(unrelated, clock.UtcNow.AddDays(-20));

        (await store.PurgeExpiredAsync()).Should().Be(1);
        (await store.PurgeExpiredAsync()).Should().Be(0);

        File.Exists(expiredPath).Should().BeFalse();
        Action describe = () => store.Describe(expired.FileReference);
        describe.Should().Throw<ConnectorAccessException>().Where(error => error.Code == "ContentExpired");
        await using Stream retained = await store.OpenAsync(current.FileReference);
        retained.ReadByte().Should().Be(2);
        (await File.ReadAllTextAsync(unrelated)).Should().Be("Retained document");
    }

    public void Dispose()
    {
        string target = Path.GetFullPath(_root);
        string temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (target.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) Directory.Delete(target, recursive: true);
    }

    private sealed class TestClock : IClock { public DateTime UtcNow { get; set; } }
}
