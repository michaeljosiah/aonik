using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

using Aonik.IntegrationTests.Support;
using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.Platform.Contracts.Services.ContactEnquiries;
using Aonik.Platform.Entities.ContactEnquiries;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.ContactEnquiries;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public class ContactEnquirySqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task ConcurrentIdenticalSubmissions_Should_KeepOneReceiptTwoEventsAndOnlyWinningPrivateImage()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var barrier = new ConcurrentSubmissionBarrier();
        var storage = new PrivateFiles();
        await using var first = Context(tenantId, barrier);
        await using var second = Context(tenantId, barrier);
        var request = Request();

        var receipts = await Task.WhenAll(Service(first, tenantId, storage).SubmitAsync(request),
            Service(second, tenantId, storage).SubmitAsync(request));

        receipts[0].Should().Be(receipts[1]);
        barrier.Arrivals.Should().Be(2);
        await using var verify = Context(tenantId);
        var stored = await verify.ContactEnquiries.Include(x => x.Images).SingleAsync();
        stored.Id.Should().Be(receipts[0].Id);
        stored.RowVersion.Should().HaveCount(8);
        stored.Images.Should().ContainSingle();
        (await verify.Set<OutboxMessage>().CountAsync(x => x.TenantId == tenantId)).Should().Be(2);
        storage.Blobs.Keys.Should().Equal(stored.Images.Single().StorageKey);
        storage.Deleted.Should().ContainSingle().Which.Should().NotBe(stored.Images.Single().StorageKey);

        // The failed INSERT graph cannot be flushed later on its original scoped context.
        first.Roles.Add(new Role { TenantId = tenantId, Name = "Unrelated first " + Guid.NewGuid() });
        second.Roles.Add(new Role { TenantId = tenantId, Name = "Unrelated second " + Guid.NewGuid() });
        await first.SaveChangesAsync();
        await second.SaveChangesAsync();
        (await verify.ContactEnquiries.CountAsync()).Should().Be(1);
        (await verify.Set<OutboxMessage>().CountAsync(x => x.TenantId == tenantId)).Should().Be(2);

        var otherTenant = Guid.NewGuid();
        await using var other = Context(otherTenant);
        var otherReceipt = await Service(other, otherTenant, storage).SubmitAsync(request);
        otherReceipt.Id.Should().NotBe(receipts[0].Id);
        (await other.ContactEnquiries.CountAsync()).Should().Be(1);
    }

    [SkippableFact]
    public async Task ConcurrentChangedPayload_Should_RejectLoserAndPreserveOnlyAcceptedMessageAndImages()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var storage = new PrivateFiles();
        var barrier = new ConcurrentSubmissionBarrier();
        await using var first = Context(tenantId, barrier);
        await using var second = Context(tenantId, barrier);
        var request = Request();
        async Task<bool> Submit(ContactEnquiryService service, ContactEnquirySubmission submission)
        {
            try { await service.SubmitAsync(submission); return true; }
            catch (ContactEnquiryConflictException) { return false; }
        }

        var results = await Task.WhenAll(Submit(Service(first, tenantId, storage), request),
            Submit(Service(second, tenantId, storage), request with { Message = "This is a different message." }));

        results.Count(x => x).Should().Be(1);
        await using var verify = Context(tenantId);
        (await verify.ContactEnquiries.SingleAsync()).Message.Should().BeOneOf(request.Message, "This is a different message.");
        (await verify.ContactEnquiryAttachments.CountAsync()).Should().Be(1);
        (await verify.Set<OutboxMessage>().CountAsync(x => x.TenantId == tenantId)).Should().Be(2);
        storage.Blobs.Should().HaveCount(1);
        storage.Deleted.Should().HaveCount(1);
    }

    [SkippableFact]
    public async Task LostSaveResponse_Should_RecognizeCommittedSubmissionAndNeverDeleteAcceptedPhoto()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var storage = new PrivateFiles();
        await using var context = Context(tenantId, new LostSaveResponse());
        var request = Request();
        var service = Service(context, tenantId, storage);

        var receipt = await service.SubmitAsync(request);
        (await service.SubmitAsync(request)).Should().Be(receipt);

        await using var verify = Context(tenantId);
        var stored = await verify.ContactEnquiries.Include(x => x.Images).SingleAsync();
        stored.Id.Should().Be(receipt.Id);
        (await verify.Set<OutboxMessage>().CountAsync(x => x.TenantId == tenantId)).Should().Be(2);
        storage.Blobs.Keys.Should().Equal(stored.Images.Single().StorageKey);
        storage.Deleted.Should().BeEmpty();
    }

    private void RequireSqlServer() => Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server LocalDB unavailable.");

    private PlatformDbContext Context(Guid tenantId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>(database.CreateOptions<PlatformDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), new Clock());
    }

    private static ContactEnquiryService Service(PlatformDbContext context, Guid tenantId, PrivateFiles files)
    {
        var settings = new Mock<ITenantSettingStore>();
        settings.Setup(x => x.GetTenantValueAsync(ContactEnquirySettingNames.Configuration, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new ContactEnquiriesConfiguration(true,
                new Dictionary<string, string> { ["order"] = "orders@example.test" }, "https://admin.example.test"),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var images = new Mock<IContactImageProcessor>();
        images.Setup(x => x.ProcessAsync(It.IsAny<ContactImageUpload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedContactImage([1, 2, 3], "image/jpeg", "image.jpg"));
        return new(context, new TestTenantProvider(tenantId), new Clock(), settings.Object, images.Object, files.Store.Object,
            Mock.Of<ITemplatedEmailSender>(), new TestCurrentUserProvider(), Mock.Of<IPermissionService>());
    }

    private static ContactEnquirySubmission Request() => new(Guid.NewGuid(), "Test customer", "customer@example.test", "order", "unverified-42",
        "Please help with this order.", [new("image.jpg", "image/jpeg", [4, 5, 6])]);

    private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UtcNow; }

    private sealed class PrivateFiles
    {
        public Mock<IFileStore> Store { get; } = new();
        public ConcurrentDictionary<string, byte[]> Blobs { get; } = new();
        public ConcurrentBag<string> Deleted { get; } = [];
        public PrivateFiles()
        {
            Store.Setup(x => x.UploadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async (Guid tenant, Guid owner, Stream input, string name, string contentType, CancellationToken ct) =>
                {
                    using var buffer = new MemoryStream();
                    await input.CopyToAsync(buffer, ct);
                    var bytes = buffer.ToArray();
                    var key = $"{tenant}/{owner}/{Guid.NewGuid()}";
                    Blobs.TryAdd(key, bytes);
                    return new FileUploadResult("Private", null, key, contentType, name, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
                });
            Store.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string key, CancellationToken ct) => { Blobs.TryRemove(key, out _); Deleted.Add(key); return Task.CompletedTask; });
        }
    }

    private sealed class ConcurrentSubmissionBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public int Arrivals => _arrivals;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<ContactEnquiry>().Any(x => x.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class LostSaveResponse : SaveChangesInterceptor
    {
        private bool _fired;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (!_fired) { _fired = true; throw new IOException("Injected lost database response"); }
            return ValueTask.FromResult(result);
        }
    }
}
