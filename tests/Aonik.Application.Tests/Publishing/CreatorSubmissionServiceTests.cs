using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Publishing;

public class CreatorSubmissionServiceTests
{
    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid PartyId { get; } = Guid.NewGuid();
        public TestCurrentUserContext Actor { get; }
        public Mock<ICurrentPartyResolver> Parties { get; } = new();
        public Mock<IPermissionService> Permissions { get; } = new();
        public Mock<IModuleGate> Gate { get; } = new();
        public PublishingOptions Options { get; }
        public PublishingDbContext Db { get; }
        public CreatorSubmissionService Service { get; }

        public Harness()
        {
            Actor = new TestCurrentUserContext
            {
                UserId = Guid.NewGuid(), TenantId = TenantId, IsAuthenticated = true, Roles = ["PersonalUser"],
            };
            Parties.Setup(x => x.GetCurrentPartyIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(PartyId);
            Options = new PublishingOptions { Channels = { ["arke-worlds"] = new PublishingChannelOptions
            {
                TenantId = TenantId, Enabled = true, MaxPackageBytes = 100_000_000, RightsDeclarationVersion = "test-v1",
            } } };
            Db = new PublishingDbContext(new DbContextOptionsBuilder<PublishingDbContext>()
                .UseInMemoryDatabase($"Publishing_{Guid.NewGuid()}").Options,
                new TestTenantProvider(TenantId), new TestCurrentUserProvider(Actor.UserId!.Value));
            Service = new CreatorSubmissionService(Db, new TestTenantProvider(TenantId), Actor,
                Parties.Object, Permissions.Object, Gate.Object, Microsoft.Extensions.Options.Options.Create(Options));
        }
        public void Dispose() => Db.Dispose();
    }

    private static CreateSubmissionRequest Request() => new(Guid.NewGuid(), "arke-worlds", "The Lantern Garden",
        "Example Creator", "An illustrated edition.", "en", "lantern-garden", "2026-11-03",
        "lantern-garden.zip", 1024, new string('a', 64), "test-v1", true);

    [Fact]
    public async Task Create_Should_PersistPrivateUnverifiedReceiptAndRevision()
    {
        using var h = new Harness();
        var result = await h.Service.CreateAsync(Request());
        result.Inserted.Should().BeTrue();
        result.Submission.Status.Should().Be(SubmissionStatuses.AwaitingUpload);
        result.Submission.Revision.PackageVerified.Should().BeFalse();
        result.Submission.Revision.RequestDigest.Should().HaveLength(64);
        var row = await h.Db.Submissions.SingleAsync();
        row.CreatorPartyId.Should().Be(h.PartyId);
        row.OriginatingUserId.Should().Be(h.Actor.UserId!.Value);
        row.TenantId.Should().Be(h.TenantId);
        row.CreatedBy.Should().Be(h.Actor.UserId);
        (await h.Db.Revisions.SingleAsync()).Id.Should().Be(row.CurrentRevisionId);
    }

    [Fact]
    public async Task Create_Should_ReplayNormalizedIdenticalRequest()
    {
        using var h = new Harness();
        var request = Request();
        var first = await h.Service.CreateAsync(request);
        var second = await h.Service.CreateAsync(request with
        {
            Title = "  " + request.Title + "  ", DeclaredPackageSha256 = request.DeclaredPackageSha256.ToUpperInvariant(),
        });
        second.Inserted.Should().BeFalse();
        second.Submission.Should().BeEquivalentTo(first.Submission);
        (await h.Db.Submissions.CountAsync()).Should().Be(1);
        (await h.Db.Revisions.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("creator")]
    [InlineData("summary")]
    [InlineData("language")]
    [InlineData("world")]
    [InlineData("edition")]
    [InlineData("filename")]
    [InlineData("bytes")]
    [InlineData("hash")]
    [InlineData("rights")]
    public async Task Create_Should_RejectChangedMaterialUnderSameRequestId(string change)
    {
        using var h = new Harness();
        var request = Request();
        await h.Service.CreateAsync(request);
        var changed = change switch
        {
            "title" => request with { Title = "Another title" },
            "creator" => request with { CreatorDisplayName = "Another creator" },
            "summary" => request with { Summary = "Another summary" },
            "language" => request with { Language = "fr" },
            "world" => request with { WorldId = "another-world" },
            "edition" => request with { Edition = "another-edition" },
            "filename" => request with { PackageFileName = "another.zip" },
            "bytes" => request with { DeclaredPackageBytes = 2048 },
            "hash" => request with { DeclaredPackageSha256 = new string('b', 64) },
            "rights" => request with { RightsDeclarationVersion = "test-v2" },
            _ => throw new InvalidOperationException(),
        };
        if (change == "rights") h.Options.Channels["arke-worlds"].RightsDeclarationVersion = "test-v2";
        var action = () => h.Service.CreateAsync(changed);
        (await action.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("request-id-reused");
        (await h.Db.Revisions.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("anonymous", "unauthenticated")]
    [InlineData("no-party", "no-party")]
    [InlineData("wrong-tenant", "invalid-creator-scope")]
    [InlineData("read-only", "submit-forbidden")]
    public async Task Create_Should_DenyMissingCreatorAuthority(string scenario, string code)
    {
        using var h = new Harness();
        if (scenario == "anonymous") h.Actor.IsAuthenticated = false;
        if (scenario == "no-party") h.Parties.Setup(x => x.GetCurrentPartyIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        if (scenario == "wrong-tenant") h.Actor.TenantId = Guid.NewGuid();
        if (scenario == "read-only") h.Actor.Roles = ["ReadOnly"];
        var action = () => h.Service.CreateAsync(Request());
        (await action.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be(code);
        (await h.Db.Submissions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_Should_AllowExplicitSubmitPermissionForStaff()
    {
        using var h = new Harness();
        h.Actor.Roles = ["Operations"];
        h.Permissions.Setup(x => x.HasPermissionAsync(h.Actor.UserId!.Value, "Publishing.Submit", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await h.Service.CreateAsync(Request())).Inserted.Should().BeTrue();
    }

    [Fact]
    public async Task GetAndList_Should_HideAnotherCreatorWithinSameTenant()
    {
        using var h = new Harness();
        var request = Request();
        var first = await h.Service.CreateAsync(request);
        h.Parties.Setup(x => x.GetCurrentPartyIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        (await h.Service.GetAsync(first.Submission.Id)).Should().BeNull();
        (await h.Service.ListAsync()).Items.Should().BeEmpty();
        var other = await h.Service.CreateAsync(request);
        other.Inserted.Should().BeTrue("request identity is scoped to creator, not shared by the tenant");
        other.Submission.Id.Should().NotBe(first.Submission.Id);
    }

    [Theory]
    [InlineData("absent", "channel-unavailable")]
    [InlineData("disabled", "channel-unavailable")]
    [InlineData("foreign", "channel-unavailable")]
    [InlineData("unconfigured", "channel-not-configured")]
    [InlineData("too-large", "package-too-large")]
    [InlineData("rights", "rights-version-mismatch")]
    public async Task Create_Should_EnforceCurrentChannelAdmission(string scenario, string code)
    {
        using var h = new Harness();
        var channel = h.Options.Channels["arke-worlds"];
        if (scenario == "absent") h.Options.Channels.Clear();
        if (scenario == "disabled") channel.Enabled = false;
        if (scenario == "foreign") channel.TenantId = Guid.NewGuid();
        if (scenario == "unconfigured") channel.MaxPackageBytes = 0;
        if (scenario == "too-large") channel.MaxPackageBytes = 100;
        if (scenario == "rights") channel.RightsDeclarationVersion = "test-v2";
        var action = () => h.Service.CreateAsync(Request());
        (await action.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be(code);
        (await h.Db.Submissions.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("missing-id")]
    [InlineData("hash")]
    [InlineData("path")]
    [InlineData("no-consent")]
    [InlineData("empty-title")]
    [InlineData("null-title")]
    [InlineData("length")]
    [InlineData("control")]
    public async Task Create_Should_RejectInvalidDeclarations(string scenario)
    {
        using var h = new Harness();
        var request = Request();
        request = scenario switch
        {
            "missing-id" => request with { ClientRequestId = Guid.Empty },
            "hash" => request with { DeclaredPackageSha256 = "not-a-hash" },
            "path" => request with { PackageFileName = "../private.zip" },
            "no-consent" => request with { DistributionPermissionGranted = false },
            "empty-title" => request with { Title = " " },
            "null-title" => request with { Title = null! },
            "length" => request with { DeclaredPackageBytes = 0 },
            "control" => request with { CreatorDisplayName = "name\0hidden" },
            _ => throw new InvalidOperationException(),
        };
        var action = () => h.Service.CreateAsync(request);
        (await action.Should().ThrowAsync<PublishingException>()).Which.Status.Should().Be(422);
    }

    [Fact]
    public async Task Create_Should_RecheckAdmissionOnReplay()
    {
        using var h = new Harness();
        var request = Request();
        await h.Service.CreateAsync(request);
        h.Options.Channels["arke-worlds"].Enabled = false;
        var action = () => h.Service.CreateAsync(request);
        (await action.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("channel-unavailable");
    }

    [Fact]
    public async Task Create_Should_DenyDisabledModuleEvenOutsideHttp()
    {
        using var h = new Harness();
        h.Gate.Setup(x => x.EnsureEnabledAsync(h.TenantId, ModuleIds.Publishing, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ModuleDisabledException(ModuleIds.Publishing));
        var action = () => h.Service.CreateAsync(Request());
        await action.Should().ThrowAsync<ModuleDisabledException>();
        (await h.Db.Submissions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_Should_NotReuseRetiredRequestIdentity()
    {
        using var h = new Harness();
        var request = Request();
        await h.Service.CreateAsync(request);
        (await h.Db.Submissions.SingleAsync()).IsDeleted = true;
        await h.Db.SaveChangesAsync();
        var action = () => h.Service.CreateAsync(request);
        (await action.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("request-id-retired");
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 101)]
    [InlineData(10001, 25)]
    public async Task List_Should_RejectUnboundedPaging(int page, int size)
    {
        using var h = new Harness();
        var action = () => h.Service.ListAsync(page, size);
        (await action.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("invalid-page");
    }
}
