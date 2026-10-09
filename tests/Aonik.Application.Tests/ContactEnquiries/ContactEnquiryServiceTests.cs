using System.Security.Cryptography;
using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

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

namespace Aonik.Application.Tests.ContactEnquiries;

public class ContactEnquiryServiceTests
{
    [Theory]
    [InlineData("order")]
    [InlineData("new")]
    [InlineData("dish")]
    [InlineData("delivery")]
    [InlineData("gift")]
    [InlineData("other")]
    public async Task Submit_Should_PersistTopicAndOnlyItsOrderReference_AndQueueTwoPrivateReferences(string topic)
    {
        using var h = new Harness();
        var request = h.Request() with { Topic = topic, OrderNumber = " OLD-42 " };

        var receipt = await h.Service.SubmitAsync(request);

        var row = await h.Db.ContactEnquiries.SingleAsync();
        row.Id.Should().Be(receipt.Id);
        row.Name.Should().Be("Alex Customer");
        row.Email.Should().Be("alex@example.test");
        row.OrderNumber.Should().Be(topic == "order" ? "OLD-42" : null);
        row.StaffRecipientEmail.Should().Be(topic + "@example.test");
        row.StaffDetailUrl.Should().Be("https://admin.example.test/contact-enquiries/" + receipt.Id);
        var events = await h.Db.Set<OutboxMessage>().ToListAsync();
        events.Should().HaveCount(2);
        events.Select(x => x.EventId).Distinct().Should().HaveCount(2);
        events.Should().OnlyContain(x => x.TenantId == h.TenantId && !x.Payload.Contains(request.Message)
            && !x.Payload.Contains("alex@example.test") && !x.Payload.Contains("OLD-42"));
        (await h.Db.Users.CountAsync()).Should().Be(0);
        (await h.Db.SignupSubscriptions.CountAsync()).Should().Be(0);
        h.Email.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Replay_Should_ReturnOriginalReceiptAfterConfigurationDisables_WithoutProcessingOrMailAgain()
    {
        using var h = new Harness();
        var request = h.Request(images: 1);
        var receipt = await h.Service.SubmitAsync(request);
        h.Configuration = null;

        (await h.Service.SubmitAsync(request)).Should().Be(receipt);

        h.Processor.Verify(x => x.ProcessAsync(It.IsAny<ContactImageUpload>(), It.IsAny<CancellationToken>()), Times.Once);
        (await h.Db.Set<OutboxMessage>().CountAsync()).Should().Be(2);
        var changed = () => h.Service.SubmitAsync(request with { Message = "This is a different enquiry." });
        await changed.Should().ThrowAsync<ContactEnquiryConflictException>();
        h.Blobs.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"isEnabled\":true,\"topicRecipients\":{},\"adminOrigin\":\"https://admin.example.test\"}")]
    [InlineData("{\"isEnabled\":true,\"topicRecipients\":{\"order\":\"support@example.test\"},\"adminOrigin\":\"http://admin.example.test\"}")]
    public async Task MissingOrUnsafeRouting_Should_BeUnavailableBeforeProcessing(string? configuration)
    {
        using var h = new Harness { Configuration = configuration };

        var submit = () => h.Service.SubmitAsync(h.Request(images: 1));

        await submit.Should().ThrowAsync<ContactEnquiryUnavailableException>();
        h.Processor.Verify(x => x.ProcessAsync(It.IsAny<ContactImageUpload>(), It.IsAny<CancellationToken>()), Times.Never);
        (await h.Db.ContactEnquiries.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("short", "bad-email", "unknown", "message")]
    [InlineData("long", "alex@example.test", "order", "message")]
    [InlineData("valid", "Alex <alex@example.test>", "order", "email")]
    [InlineData("valid", "alex@example.test", "unknown", "topic")]
    public async Task InvalidFields_Should_RejectBeforeAnyFileProcessing(string message, string email, string topic, string field)
    {
        using var h = new Harness();
        var request = h.Request(images: 1) with
        {
            Message = message == "long" ? new string('x', 5001) : message == "short" ? "short" : "A valid enquiry message.",
            Email = email, Topic = topic
        };

        var submit = () => h.Service.SubmitAsync(request);

        (await submit.Should().ThrowAsync<ContactEnquiryValidationException>()).Which.FieldErrors.Should().ContainKey(field);
        h.Processor.Verify(x => x.ProcessAsync(It.IsAny<ContactImageUpload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FileLimits_Should_ReportSafeIndexedProblemsAndAcceptNoPartialEnquiry()
    {
        using var h = new Harness();
        var request = h.Request() with { Images = [new("../empty.jpg", "image/jpeg", []), new("too-big.jpg", "image/jpeg", new byte[10 * 1024 * 1024 + 1])] };

        var submit = () => h.Service.SubmitAsync(request);

        var failure = (await submit.Should().ThrowAsync<ContactEnquiryValidationException>()).Which;
        failure.ImageProblems.Select(x => x.Index).Should().Equal(0, 1);
        failure.ImageProblems[0].FileName.Should().Be("empty.jpg");
        h.Blobs.Should().BeEmpty();
        (await h.Db.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MixedValidAndRejectedImages_Should_ProcessAllButUploadNone()
    {
        using var h = new Harness();
        h.Processor.Setup(x => x.ProcessAsync(It.Is<ContactImageUpload>(image => image.FileName == "image-1.jpg"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ContactImageValidationException("infected", "This image could not be accepted."));

        var submit = () => h.Service.SubmitAsync(h.Request(images: 3));

        var failure = (await submit.Should().ThrowAsync<ContactEnquiryValidationException>()).Which;
        failure.ImageProblems.Should().ContainSingle().Which.Index.Should().Be(1);
        h.Processor.Verify(x => x.ProcessAsync(It.IsAny<ContactImageUpload>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        h.Blobs.Should().BeEmpty();
        (await h.Db.ContactEnquiries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UploadFailure_Should_RemoveOnlyEarlierUploads_WithoutAcceptingOrLeakingRawFailure()
    {
        using var h = new Harness { FailUploadNumber = 2 };

        var submit = () => h.Service.SubmitAsync(h.Request(images: 2));

        (await submit.Should().ThrowAsync<ContactEnquiryUnavailableException>()).Which.Message.Should().NotContain("private provider detail");
        h.Blobs.Should().BeEmpty();
        h.Deleted.Should().HaveCount(1);
        (await h.Db.ContactEnquiries.CountAsync()).Should().Be(0);
        (await h.Db.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task FailedSave_Should_DetachRejectedRowsAndMailBeforeAnUnrelatedSave()
    {
        using var h = new Harness(new SaveFailure(afterCommit: false));
        var submit = () => h.Service.SubmitAsync(h.Request(images: 1));

        await submit.Should().ThrowAsync<ContactEnquiryUnavailableException>();
        h.Db.Roles.Add(new Role { TenantId = h.TenantId, Name = "Unrelated role" });
        await h.Db.SaveChangesAsync();

        (await h.Db.ContactEnquiries.CountAsync()).Should().Be(0);
        (await h.Db.ContactEnquiryAttachments.CountAsync()).Should().Be(0);
        (await h.Db.Set<OutboxMessage>().CountAsync()).Should().Be(0);
        h.Blobs.Should().BeEmpty();
    }

    [Fact]
    public async Task LostCommitResponse_Should_ReturnDurableReceiptAndPreserveImages()
    {
        using var h = new Harness(new SaveFailure(afterCommit: true));

        var receipt = await h.Service.SubmitAsync(h.Request(images: 1));

        (await h.Db.ContactEnquiries.SingleAsync()).Id.Should().Be(receipt.Id);
        (await h.Db.Set<OutboxMessage>().CountAsync()).Should().Be(2);
        h.Blobs.Should().HaveCount(1);
        h.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task Delivery_Should_UseFrozenStaffRoute_AndAcknowledgementCannotRelaySubmittedContent()
    {
        using var h = new Harness();
        var receipt = await h.Service.SubmitAsync(h.Request() with { Message = "<a href='https://attacker.test'>click me</a>" });
        h.Configuration = null;

        await h.Service.DeliverAsync(receipt.Id, "staff");
        await h.Service.DeliverAsync(receipt.Id, "acknowledgement");

        h.Email.Messages[0].To.Should().Be("order@example.test");
        h.Email.Messages[0].Model.Should().ContainKey("message");
        var acknowledgement = h.Email.Messages[1];
        acknowledgement.To.Should().Be("alex@example.test");
        acknowledgement.Model.Keys.Should().BeEquivalentTo("enquiry_id", "received_at");
        JsonSerializer.Serialize(acknowledgement.Model).Should().NotContain("attacker.test");
    }

    [Fact]
    public async Task AdminReadsAndDelivery_Should_RespectTenantAndPermission_WithNoPublicStorageUrls()
    {
        using var h = new Harness();
        var receipt = await h.Service.SubmitAsync(h.Request(images: 1));
        var detail = await h.Service.GetAsync(receipt.Id);
        detail!.Images.Should().HaveCount(1);
        var image = await h.Service.OpenImageAsync(receipt.Id, detail.Images[0].Id);
        using (image!.Content) image.Content.Length.Should().Be(3);
        var foreign = h.CreateService(Guid.NewGuid());

        (await foreign.GetAsync(receipt.Id)).Should().BeNull();
        (await foreign.OpenImageAsync(receipt.Id, detail.Images[0].Id)).Should().BeNull();
        (await foreign.ListAsync()).Items.Should().BeEmpty();
        await foreign.DeliverAsync(receipt.Id, "staff");
        h.Email.Messages.Should().BeEmpty();
        h.Permissions.Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), "Customers.Read", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var denied = () => h.Service.GetAsync(receipt.Id);
        await denied.Should().ThrowAsync<PermissionDeniedException>();
        h.Files.Verify(x => x.GetUrl(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PrivateImageReadFailure_Should_ReturnSafeUnavailable_WhilePreservingCallerCancellation()
    {
        using var h = new Harness();
        var receipt = await h.Service.SubmitAsync(h.Request(images: 1));
        var imageId = (await h.Service.GetAsync(receipt.Id))!.Images.Single().Id;
        h.Files.Setup(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("private/container/customer-photo"));

        var read = () => h.Service.OpenImageAsync(receipt.Id, imageId);

        (await read.Should().ThrowAsync<ContactEnquiryUnavailableException>()).Which.Message.Should().NotContain("private/container");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        h.Files.Setup(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancelled.Token));
        var cancel = () => h.Service.OpenImageAsync(receipt.Id, imageId, cancelled.Token);
        await cancel.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public PlatformDbContext Db { get; }
        public Mock<IContactImageProcessor> Processor { get; } = new();
        public Mock<IFileStore> Files { get; } = new();
        public Mock<IPermissionService> Permissions { get; } = new();
        public Dictionary<string, byte[]> Blobs { get; } = [];
        public List<string> Deleted { get; } = [];
        public CapturingEmail Email { get; } = new();
        public ContactEnquiryService Service { get; }
        public string? Configuration { get; set; } = JsonSerializer.Serialize(new ContactEnquiriesConfiguration(true,
            new[] { "order", "new", "dish", "delivery", "gift", "other" }.ToDictionary(x => x, x => x + "@example.test"), "https://admin.example.test"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        public int? FailUploadNumber { get; init; }
        private int _uploads;

        public Harness(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase("ContactEnquiries_" + Guid.NewGuid());
            if (interceptor is not null) options.AddInterceptors(interceptor);
            Db = new(options.Options, new TestTenantProvider(TenantId), new TestCurrentUserProvider(), new FixedClock());
            Processor.Setup(x => x.ProcessAsync(It.IsAny<ContactImageUpload>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ContactImageUpload image, CancellationToken _) => new ProcessedContactImage([1, 2, 3], "image/jpeg", image.FileName));
            Files.Setup(x => x.UploadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async (Guid tenantId, Guid ownerId, Stream stream, string fileName, string contentType, CancellationToken ct) =>
                {
                    if (++_uploads == FailUploadNumber) throw new IOException("private provider detail");
                    using var content = new MemoryStream();
                    await stream.CopyToAsync(content, ct);
                    var bytes = content.ToArray();
                    var key = tenantId + "/" + ownerId + "/" + Guid.NewGuid();
                    Blobs.Add(key, bytes);
                    return new FileUploadResult("Private", null, key, contentType, fileName, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
                });
            Files.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string key, CancellationToken _) =>
            { Deleted.Add(key); Blobs.Remove(key); return Task.CompletedTask; });
            Files.Setup(x => x.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken _) => Blobs.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : (Stream?)null);
            Permissions.Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), "Customers.Read", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Service = CreateService(TenantId);
        }

        public ContactEnquiryService CreateService(Guid tenantId)
        {
            var settings = new Mock<ITenantSettingStore>();
            settings.Setup(x => x.GetTenantValueAsync(ContactEnquirySettingNames.Configuration, tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(() => Configuration);
            return new(Db, new TestTenantProvider(tenantId), new FixedClock(), settings.Object, Processor.Object, Files.Object,
                Email, new TestCurrentUserProvider(), Permissions.Object);
        }

        public ContactEnquirySubmission Request(int images = 0) => new(Guid.NewGuid(), " Alex Customer ", "ALEX@EXAMPLE.TEST", "order", null,
            "Please help with my existing order.", Enumerable.Range(0, images).Select(i => new ContactImageUpload($"image-{i}.jpg", "image/jpeg", [4, 5, 6])).ToList());
        public void Dispose() => Db.Dispose();
    }

    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class CapturingEmail : ITemplatedEmailSender
    {
        public List<TemplatedEmailMessage> Messages { get; } = [];
        public Task SendAsync(TemplatedEmailMessage message, CancellationToken cancellationToken = default)
        { Messages.Add(message); return Task.CompletedTask; }
    }

    private sealed class SaveFailure(bool afterCommit) : SaveChangesInterceptor
    {
        private bool _fired;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!afterCommit && !_fired && eventData.Context!.ChangeTracker.Entries<ContactEnquiry>().Any(x => x.State == EntityState.Added))
            { _fired = true; throw new DbUpdateException("Injected write failure"); }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (afterCommit && !_fired)
            { _fired = true; throw new IOException("Injected lost commit response"); }
            return ValueTask.FromResult(result);
        }
    }
}
