using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;

using Aonik.Infrastructure.Publishing;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Publishing;

public class PublicationIntakeTests
{
    private sealed class Clock : IClock { public DateTime UtcNow { get; set; } = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class Harness : IDisposable
    {
        public readonly Guid Tenant = Guid.NewGuid();
        public Guid Party = Guid.NewGuid();
        public readonly string Root = Path.Combine(Path.GetTempPath(), "aonik-intake-tests-" + Guid.NewGuid().ToString("N"));
        public readonly Clock Clock = new();
        public readonly PublishingOptions Settings;
        public readonly PublishingDbContext Db;
        public readonly PrivatePublicationObjectStore Store;
        public readonly CreatorSubmissionService Creator;
        public readonly PublicationIntakeService Intake;
        public readonly TestTenantProvider TenantProvider;

        public Harness()
        {
            TenantProvider = new TestTenantProvider(Tenant);
            var actor = new TestCurrentUserContext { UserId = Guid.NewGuid(), TenantId = Tenant, Roles = ["PersonalUser"], IsAuthenticated = true };
            var parties = new Mock<ICurrentPartyResolver>();
            parties.Setup(p => p.GetCurrentPartyIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Party);
            Settings = new PublishingOptions
            {
                Intake = new PublishingIntakeOptions { PrivateRoot = Path.Combine(Root, "private"), MaxCreatorRetainedBytes = 1024,
                    MaxTenantRetainedBytes = 4096, PartSizeBytes = 4, SingleShotThresholdBytes = 8, SessionHours = 1, CleanupGraceHours = 1 },
                Verifier = new PackageVerifierOptions { ReaderCommit = DockerPackageVerifier.ReaderPin, Image = "example/verifier@sha256:" + new string('a',64) },
                Channels = { ["arke-worlds"] = new PublishingChannelOptions { TenantId = Tenant, Enabled = true,
                    NamespaceAdmissionEnabled = true, MaxPackageBytes = 1000, RightsDeclarationVersion = "test-v1" } },
            };
            Db = new PublishingDbContext(new DbContextOptionsBuilder<PublishingDbContext>()
                .UseInMemoryDatabase("intake-" + Guid.NewGuid()).Options, TenantProvider, new TestCurrentUserProvider(actor.UserId!.Value), Clock);
            Creator = new CreatorSubmissionService(Db, TenantProvider, actor, parties.Object, Mock.Of<IPermissionService>(),
                Mock.Of<IModuleGate>(), Microsoft.Extensions.Options.Options.Create(Settings));
            var host = Mock.Of<IHostEnvironment>(e => e.ContentRootPath == Path.Combine(Root,"host"));
            Store = new PrivatePublicationObjectStore(Microsoft.Extensions.Options.Options.Create(Settings), host, new ConfigurationBuilder().Build());
            Intake = new PublicationIntakeService(Db, Creator, Store, Microsoft.Extensions.Options.Options.Create(Settings), Clock);
        }
        public async Task<SubmissionReceipt> Draft(byte[] bytes, string? declaredHash = null)
            => (await Creator.CreateAsync(new CreateSubmissionRequest(Guid.NewGuid(), "arke-worlds", "The Lantern Garden", "Creator", "Edition", "en",
                "lantern-garden", "2026-11-03", "edition.zip", bytes.Length, declaredHash ?? Hash(bytes), "test-v1", true))).Submission;
        public async Task<UploadReceipt> Upload(SubmissionReceipt receipt, byte[] bytes)
        {
            var session = await Intake.BeginAsync(receipt.Id);
            for (var p = 1; p <= session.TotalParts; p++)
            {
                var part = bytes.Skip((p-1)*session.PartSizeBytes).Take(session.PartSizeBytes).ToArray();
                await Intake.PutPartAsync(receipt.Id, session.Id, p, Hash(part), part.Length, new MemoryStream(part));
            }
            return await Intake.CompleteAsync(receipt.Id, session.Id);
        }
        public PublicationValidationHandler Handler(IPackageVerifier verifier) => new(Db, Store, verifier, TenantProvider, Clock, Microsoft.Extensions.Options.Options.Create(Settings));
        public PublicationUploadMaintenance Maintenance(IPublishingObjectStore? store = null) => new(Db, store ?? Store, TenantProvider, Clock, Microsoft.Extensions.Options.Options.Create(Settings));
        public async Task<PublicationValidationRequestedEvent> Event() => JsonSerializer.Deserialize<PublicationValidationRequestedEvent>((await Db.Set<OutboxMessage>().SingleAsync()).Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        public void Dispose()
        {
            Db.Dispose();
            var full = Path.GetFullPath(Root);
            if (!full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Fact]
    public async Task Multipart_Should_ResumeVerifyAndReplayWithoutDoubleCounting()
    {
        using var h = new Harness(); var bytes = Encoding.UTF8.GetBytes("twelve-bytes!");
        var draft = await h.Draft(bytes); var upload = await h.Intake.BeginAsync(draft.Id);
        var first = bytes.Take(upload.PartSizeBytes).ToArray();
        await h.Intake.PutPartAsync(draft.Id, upload.Id, 1, Hash(first), first.Length, new MemoryStream(first));
        var resumed = await h.Intake.BeginAsync(draft.Id);
        resumed.Id.Should().Be(upload.Id); resumed.MissingParts.Should().NotContain(1);
        (await h.Intake.PutPartAsync(draft.Id, upload.Id, 1, Hash(first), first.Length, new MemoryStream(first))).ReceivedBytes.Should().Be(first.Length);
        var complete = await h.Upload(draft, bytes);
        complete.Status.Should().Be("uploaded"); complete.ReceivedBytes.Should().Be(bytes.Length);
        (await h.Intake.CompleteAsync(draft.Id, upload.Id)).Status.Should().Be("uploaded");
        (await h.Creator.GetAsync(draft.Id))!.Revision.PackageVerified.Should().BeFalse();
        await using var downloaded = (await h.Intake.DownloadAsync(draft.Id)).Content;
        (await SHA256.HashDataAsync(downloaded)).Should().Equal(SHA256.HashData(bytes));
    }

    [Fact]
    public async Task Part_Should_RejectChangedRetryAndFalseDigest()
    {
        using var h = new Harness(); var bytes = new byte[] { 1,2,3,4 };
        var draft = await h.Draft(bytes); var upload = await h.Intake.BeginAsync(draft.Id);
        await h.Intake.PutPartAsync(draft.Id, upload.Id, 1, Hash(bytes), 4, new MemoryStream(bytes));
        var changed = () => h.Intake.PutPartAsync(draft.Id, upload.Id, 1, new string('a',64), 4, new MemoryStream(bytes));
        (await changed.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("part-identity-conflict");
        var lie = () => h.Intake.PutPartAsync(draft.Id, upload.Id, 1, Hash(bytes), 4, new MemoryStream([9,9,9,9]));
        (await lie.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("upload-bytes-mismatch");
        (await h.Intake.GetAsync(draft.Id, upload.Id)).ReceivedBytes.Should().Be(4);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Part_Should_RejectShortAndExcessActualBytes(int length)
    {
        using var h = new Harness(); var bytes = new byte[] {1,2,3,4};
        var draft = await h.Draft(bytes); var upload = await h.Intake.BeginAsync(draft.Id);
        var action = () => h.Intake.PutPartAsync(draft.Id, upload.Id, 1, Hash(bytes), 4, new MemoryStream(new byte[length]));
        await action.Should().ThrowAsync<PublishingException>();
        (await h.Intake.GetAsync(draft.Id, upload.Id)).MissingParts.Should().Equal(1);
        Directory.GetFiles(h.Root, "*.upload-*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Complete_Should_RejectChangedWholeArchiveEvenWhenEachPartMatches()
    {
        using var h = new Harness(); var bytes = new byte[] {1,2,3,4};
        var draft = await h.Draft(bytes, new string('0',64)); var upload = await h.Intake.BeginAsync(draft.Id);
        await h.Intake.PutPartAsync(draft.Id, upload.Id, 1, Hash(bytes), 4, new MemoryStream(bytes));
        var action = () => h.Intake.CompleteAsync(draft.Id, upload.Id);
        (await action.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("upload-bytes-mismatch");
        (await h.Db.UploadSessions.SingleAsync()).Status.Should().Be("failed");
        (await h.Creator.GetAsync(draft.Id))!.Revision.PackageVerified.Should().BeFalse();
    }

    [Fact]
    public async Task ExpiryAndQuota_Should_PreventUnreservedUploads()
    {
        using var h = new Harness(); var draft = await h.Draft([1,2,3,4]);
        h.Settings.Intake.MaxCreatorRetainedBytes = 3;
        var denied = () => h.Intake.BeginAsync(draft.Id);
        (await denied.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("upload-quota-exceeded");
        (await h.Db.UploadSessions.CountAsync()).Should().Be(0);
        h.Settings.Intake.MaxCreatorRetainedBytes = 1024;
        var upload = await h.Intake.BeginAsync(draft.Id);
        h.Clock.UtcNow = h.Clock.UtcNow.AddHours(2);
        var expired = () => h.Intake.PutPartAsync(draft.Id, upload.Id, 1, Hash([1,2,3,4]), 4, new MemoryStream([1,2,3,4]));
        (await expired.Should().ThrowAsync<PublishingException>()).Which.Status.Should().Be(410);
    }

    [Fact]
    public async Task AnotherCreator_Should_NotReadUploadPreviewOrPackage()
    {
        using var h = new Harness(); var draft = await h.Draft([1,2,3,4]); var upload = await h.Upload(draft,[1,2,3,4]);
        h.Party = Guid.NewGuid();
        foreach (var operation in new Func<Task>[] {
            async () => { await h.Intake.GetAsync(draft.Id,upload.Id); }, async () => { await h.Intake.PreviewAsync(draft.Id); },
            async () => { await h.Intake.DownloadAsync(draft.Id); }, async () => { await h.Intake.CancelAsync(draft.Id,upload.Id); } })
            (await operation.Should().ThrowAsync<PublishingException>()).Which.Status.Should().Be(404);
    }

    [Fact]
    public async Task Submit_Should_AtomicallyQueueExactBytesAndReplayOneValidationEvent()
    {
        using var h = new Harness(); var draft = await h.Draft([1,2,3,4]); await h.Upload(draft,[1,2,3,4]);
        var request = new SubmitPackageRequest(draft.Revision.Id,Guid.NewGuid());
        var first = await h.Intake.SubmitAsync(draft.Id,request);
        (await h.Intake.SubmitAsync(draft.Id,request)).ReportId.Should().Be(first.ReportId);
        (await h.Db.Set<OutboxMessage>().CountAsync()).Should().Be(1);
        (await h.Event()).ReportId.Should().Be(first.ReportId);
        var cancel = () => h.Intake.CancelAsync(draft.Id,(h.Db.UploadSessions.Single()).Id);
        (await cancel.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("submitted-package-retained");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validation_Should_HoldUnknownOrEnterReviewWithoutApproving(bool passed)
    {
        using var h = new Harness(); var bytes = new byte[] {1,2,3,4}; var draft = await h.Draft(bytes); await h.Upload(draft,bytes);
        await h.Intake.SubmitAsync(draft.Id,new(draft.Revision.Id,Guid.NewGuid()));
        var verifier = new Mock<IPackageVerifier>();
        verifier.Setup(v => v.VerifyAsync(It.IsAny<VerifiedInput>(),It.IsAny<PackageVerifierOptions>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageVerificationResult(passed,passed?"verified":"verifier-unavailable",DockerPackageVerifier.ReaderPin,
                Hash(bytes),4,"lantern-garden","2026-11-03",new string('b',64),"world",null,null,null,[]));
        var message = await h.Event(); await h.Handler(verifier.Object).HandleAsync(message); await h.Handler(verifier.Object).HandleAsync(message);
        var receipt = (await h.Creator.GetAsync(draft.Id))!;
        receipt.Status.Should().Be(passed?"in-review":"held"); receipt.Revision.PackageVerified.Should().Be(passed);
        (await h.Intake.PreviewAsync(draft.Id))!.Status.Should().Be(passed?"passed":"held");
        verifier.Verify(v => v.VerifyAsync(It.IsAny<VerifiedInput>(),It.IsAny<PackageVerifierOptions>(),It.IsAny<CancellationToken>()),Times.Once);
    }

    [Fact]
    public async Task Validation_Should_HoldMismatchedEditionAndUnestablishedPartLineage()
    {
        using var h = new Harness(); var bytes = new byte[] {1,2,3,4}; var draft = await h.Draft(bytes); await h.Upload(draft,bytes);
        await h.Intake.SubmitAsync(draft.Id,new(draft.Revision.Id,Guid.NewGuid()));
        var verifier = new Mock<IPackageVerifier>();
        verifier.Setup(v=>v.VerifyAsync(It.IsAny<VerifiedInput>(),It.IsAny<PackageVerifierOptions>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageVerificationResult(true,"verified",DockerPackageVerifier.ReaderPin,Hash(bytes),4,
                "lantern-garden","2026-11-03",new string('b',64),"part",new string('a',64),1,null,[]));
        await h.Handler(verifier.Object).HandleAsync(await h.Event());
        (await h.Intake.PreviewAsync(draft.Id))!.Code.Should().Be("part-lineage-unavailable");
        (await h.Creator.GetAsync(draft.Id))!.Revision.PackageVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Cleanup_Should_RetainReservationsUntilBytesAreActuallyRemoved()
    {
        using var h = new Harness(); var draft = await h.Draft([1,2,3,4]); var upload = await h.Upload(draft,[1,2,3,4]);
        await h.Intake.CancelAsync(draft.Id,upload.Id);
        (await h.Db.UploadQuotas.SumAsync(q=>q.ReservedBytes)).Should().Be(8);
        (await h.Maintenance().SweepAsync()).Should().Be(0);
        h.Clock.UtcNow = h.Clock.UtcNow.AddHours(2);
        (await h.Maintenance().SweepAsync()).Should().Be(1);
        (await h.Db.UploadQuotas.SumAsync(q=>q.ReservedBytes)).Should().Be(0);
        (await h.Maintenance().SweepAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OldCancelReplay_Should_NotDetachOrCancelTheReplacementUpload()
    {
        using var h = new Harness(); var draft = await h.Draft([1,2,3,4]);
        var old = await h.Intake.BeginAsync(draft.Id); await h.Intake.CancelAsync(draft.Id,old.Id);
        var replacement = await h.Intake.BeginAsync(draft.Id); replacement.Id.Should().NotBe(old.Id);
        await h.Intake.CancelAsync(draft.Id,old.Id);
        (await h.Db.Revisions.SingleAsync()).CurrentUploadSessionId.Should().Be(replacement.Id);
        (await h.Intake.BeginAsync(draft.Id)).Id.Should().Be(replacement.Id);
        var stale = ()=>h.Intake.CompleteAsync(draft.Id,old.Id);
        (await stale.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("upload-superseded");
    }

    [Fact]
    public async Task ExpiredAssemblyClaim_Should_AllowRecoveryAndFenceTheOldCompletion()
    {
        using var h = new Harness(); var bytes = new byte[] {1,2,3,4}; var draft = await h.Draft(bytes);
        var upload = await h.Intake.BeginAsync(draft.Id);
        await h.Intake.PutPartAsync(draft.Id,upload.Id,1,Hash(bytes),4,new MemoryStream(bytes));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = new Mock<IPublishingObjectStore>();
        storage.SetupGet(s=>s.LocationId).Returns(()=>h.Store.LocationId);
        storage.Setup(s=>s.AssembleAsync(h.Tenant,upload.Id,Hash(bytes),4,
                It.IsAny<IReadOnlyList<(int Number,string Sha256)>>(),It.IsAny<CancellationToken>()))
            .Returns(async ()=> { entered.SetResult(); await release.Task; return new VerifiedInput("unused",Hash(bytes),4); });
        var interrupted = new PublicationIntakeService(h.Db,h.Creator,storage.Object,
            Microsoft.Extensions.Options.Options.Create(h.Settings),h.Clock);
        var oldCompletion = interrupted.CompleteAsync(draft.Id,upload.Id);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(h.Settings.Intake.AssemblyMinutes+1);
        try { (await h.Intake.CompleteAsync(draft.Id,upload.Id)).Status.Should().Be("uploaded"); }
        finally { release.TrySetResult(); }
        var stale = async ()=>await oldCompletion;
        (await stale.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("assembly-claim-lost");
        (await h.Intake.GetAsync(draft.Id,upload.Id)).Status.Should().Be("uploaded");
    }

    [Fact]
    public async Task FailedDeletion_Should_RetainBytesAndQuotaUntilARecoveredSweep()
    {
        using var h = new Harness(); var draft = await h.Draft([1,2,3,4]); var upload = await h.Upload(draft,[1,2,3,4]);
        await h.Intake.CancelAsync(draft.Id,upload.Id); h.Clock.UtcNow = h.Clock.UtcNow.AddHours(2);
        var storage = new Mock<IPublishingObjectStore>();
        storage.SetupGet(s=>s.LocationId).Returns(()=>h.Store.LocationId);
        storage.Setup(s=>s.DeleteSessionAsync(h.Tenant,upload.Id,It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("busy"));
        (await h.Maintenance(storage.Object).SweepAsync()).Should().Be(0);
        (await h.Db.UploadSessions.SingleAsync()).Status.Should().Be("deleting");
        (await h.Db.UploadQuotas.SumAsync(q=>q.ReservedBytes)).Should().Be(8);
        Directory.GetFiles(h.Root,"package-*.zip",SearchOption.AllDirectories).Should().ContainSingle();
        (await h.Maintenance().SweepAsync()).Should().Be(1);
        (await h.Db.UploadQuotas.SumAsync(q=>q.ReservedBytes)).Should().Be(0);
        Directory.GetFiles(h.Root,"package-*.zip",SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("namespace-disabled")]
    [InlineData("limits-changed")]
    public async Task Validation_Should_HoldWhenTheAdmittedPolicyChanges(string change)
    {
        using var h = new Harness(); var bytes = new byte[] {1,2,3,4}; var draft = await h.Draft(bytes); await h.Upload(draft,bytes);
        await h.Intake.SubmitAsync(draft.Id,new(draft.Revision.Id,Guid.NewGuid()));
        if (change == "disabled") h.Settings.Channels["arke-worlds"].Enabled = false;
        if (change == "namespace-disabled") h.Settings.Channels["arke-worlds"].NamespaceAdmissionEnabled = false;
        if (change == "limits-changed") h.Settings.Verifier.MaxAssets--;
        var verifier = new Mock<IPackageVerifier>();
        verifier.Setup(v=>v.VerifyAsync(It.IsAny<VerifiedInput>(),It.IsAny<PackageVerifierOptions>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageVerificationResult(true,"verified",DockerPackageVerifier.ReaderPin,Hash(bytes),4,
                "lantern-garden","2026-11-03",new string('b',64),"world",null,null,null,[]));
        await h.Handler(verifier.Object).HandleAsync(await h.Event());
        (await h.Intake.PreviewAsync(draft.Id))!.Code.Should().Be("validation-policy-changed");
        (await h.Db.Submissions.SingleAsync()).Status.Should().Be("held");
        (await h.Db.Revisions.SingleAsync()).PackageVerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task Cleanup_Should_NeverDeleteUploadedOrSubmittedPackages()
    {
        using var h = new Harness(); var draft = await h.Draft([1,2,3,4]); await h.Upload(draft,[1,2,3,4]);
        await h.Intake.SubmitAsync(draft.Id,new(draft.Revision.Id,Guid.NewGuid())); h.Clock.UtcNow = h.Clock.UtcNow.AddDays(10);
        (await h.Maintenance().SweepAsync()).Should().Be(0);
        (await h.Intake.DownloadAsync(draft.Id)).Content.Dispose();
        (await h.Db.UploadQuotas.SumAsync(q=>q.ReservedBytes)).Should().Be(8);
    }

    [Fact]
    public async Task Store_Should_RefusePublicRootsAndLocationChanges()
    {
        using var h = new Harness(); var draft = await h.Draft([1,2,3,4]); var upload = await h.Intake.BeginAsync(draft.Id);
        h.Settings.Intake.PrivateRoot = Path.Combine(h.Root,"another-private-store");
        var moved = () => h.Intake.PutPartAsync(draft.Id,upload.Id,1,Hash([1,2,3,4]),4,new MemoryStream([1,2,3,4]));
        (await moved.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("storage-location-changed");
        h.Settings.Intake.PrivateRoot = Path.Combine(h.Root,"host","wwwroot");
        var publicStore = () => h.Store.StorePartAsync(h.Tenant,upload.Id,1,Hash([1,2,3,4]),4,new MemoryStream([1,2,3,4]));
        (await publicStore.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("private-storage-unavailable");
    }

    [Fact]
    public void Verifier_Should_PinImageAndDisableNetworkWritesPrivilegesAndUnboundedResources()
    {
        using var h = new Harness();
        var info = DockerPackageVerifier.BuildStartInfo(new VerifiedInput(Path.Combine(h.Root,"package.zip"),new string('a',64),4),
            h.Settings.Verifier,"aonik-verify-"+new string('a',32));
        info.UseShellExecute.Should().BeFalse();
        var args = info.ArgumentList.ToArray();
        args.Should().Contain(new[] {"--network","none","--memory","512m","--read-only","--pids-limit","--cap-drop","ALL","--security-opt","no-new-privileges","65532:65532"});
        args.Should().Contain(a=>a.StartsWith("type=bind,source=",StringComparison.Ordinal)&&a.EndsWith(",readonly",StringComparison.Ordinal));
    }
}
