using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Publishing;

public class EditorialReviewServiceTests
{
    private const string Reader = "27df122566bcdef18126932c15a74112e18b8573";
    private sealed class Harness : IDisposable
    {
        public readonly Guid Tenant = Guid.NewGuid(), CreatorUser = Guid.NewGuid(), CreatorParty = Guid.NewGuid(), EditorUser = Guid.NewGuid(), EditorParty = Guid.NewGuid();
        public readonly TestCurrentUserContext Actor = new();
        public readonly PublishingDbContext Db;
        public readonly PublishingOptions Settings;
        public readonly Mock<ICurrentPartyResolver> Parties = new();
        public readonly Mock<IUserPartyMembershipReader> Memberships = new();
        public readonly Mock<IPermissionService> Permissions = new();
        public readonly CreatorSubmissionService Creators;
        public readonly SubmissionRevisionService Revisions;
        public readonly EditorialReviewService Reviews;
        public Guid Party;
        public IReadOnlyList<Guid>? LinkedParties;
        public bool CanReview = true;
        public Harness()
        {
            var tenant = new TestTenantProvider(Tenant);
            var current = new Mock<ICurrentUserProvider>(); current.Setup(p => p.GetCurrentUserId()).Returns(() => Actor.UserId);
            Db = new(new DbContextOptionsBuilder<PublishingDbContext>().UseInMemoryDatabase("editorial-" + Guid.NewGuid()).Options, tenant, current.Object);
            Settings = new()
            {
                Verifier = new() { ReaderCommit = Reader, Image = "test/verifier@sha256:" + new string('a', 64) },
                Channels = { ["arke-worlds"]=new() {TenantId=Tenant,Enabled=true,NamespaceAdmissionEnabled=true,MaxPackageBytes=1000,
                    RightsDeclarationVersion="test-v1",ReviewEnabled=true,ReviewPolicyVersion="editorial-v1",ReviewerUserIds=[EditorUser,CreatorUser]} }
            };
            Parties.Setup(p => p.GetCurrentPartyIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Party);
            Memberships.Setup(m => m.GetPartyIdsAsync(Tenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => LinkedParties ?? [Party]);
            Permissions.Setup(p => p.HasPermissionAsync(It.IsAny<Guid>(), "Publishing.Review", It.IsAny<CancellationToken>())).ReturnsAsync(() => CanReview);
            var configuration = Microsoft.Extensions.Options.Options.Create(Settings);
            Creators = new(Db, tenant, Actor, Parties.Object, Permissions.Object, Mock.Of<IModuleGate>(), configuration);
            Revisions = new(Db, Creators, configuration);
            var authority = new EditorialReviewAuthorization(tenant, Actor, Parties.Object, Memberships.Object, Permissions.Object, Mock.Of<IModuleGate>(), configuration);
            Reviews = new(Db, authority, Mock.Of<IPublishingObjectStore>(), configuration, Mock.Of<IClock>(c => c.UtcNow == new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc)));
            AsCreator();
        }
        public void AsCreator() { Actor.IsAuthenticated = true; Actor.UserId = CreatorUser; Actor.TenantId = Tenant; Actor.Roles = ["PersonalUser"]; Party = CreatorParty; LinkedParties = null; }
        public void AsEditor() { Actor.IsAuthenticated = true; Actor.UserId = EditorUser; Actor.TenantId = Tenant; Actor.Roles = ["Operations"]; Party = EditorParty; LinkedParties = null; }
        public CreateSubmissionRequest Declaration() => new(Guid.NewGuid(), "arke-worlds", "Lantern Garden", "Creator", "A quiet adventure", "en", "lantern-garden", "edition-one",
            "edition.zip", 4, new string('b', 64), "test-v1", true, new("children", "Ages 7+", "world", "cover", "Original illustrations by creator",
                [new("Creator owns the work", "private-contract-001", ["cover"])]));
        public async Task<(SubmissionReceipt Receipt, CreateSubmissionRequest Request)> SeedAsync(string status = "passed", bool presentation = true, PublicationEditorialMaterial? material = null, bool omitMaterial = false)
        {
            AsCreator(); var request = Declaration(); if (omitMaterial) request = request with { EditorialMaterial = null }; else if (material is not null) request = request with { EditorialMaterial = material };
            var receipt = (await Creators.CreateAsync(request)).Submission;
            var revision = await Db.Revisions.SingleAsync(); var submission = await Db.Submissions.SingleAsync();
            var asset = new PackageAsset("cover", "cover.png", "image/png", 4, new string('c', 64));
            var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("edition-one\ncover.png " + new string('c', 64))));
            var manifest = JsonSerializer.SerializeToElement(new
            {
                format = "arke-world-package",
                schemaVersion = 1,
                id = request.WorldId,
                edition = request.Edition,
                name = "Lantern Garden",
                productions = new[] { new { id = "first-story", title = "Lantern story", picture = "cover" } },
                assets = new { cover = new { path = asset.Path, sha256 = asset.Sha256 } }
            });
            var package = new PackageVerificationResult(true, "verified", Reader, request.DeclaredPackageSha256, 4, request.WorldId, request.Edition, fingerprint,
                "world", null, null, "cover", [asset], presentation ? new(1, new string('d', 64), manifest) : null);
            var report = new PublicationValidationReport
            {
                TenantId = Tenant,
                SubmissionId = submission.Id,
                RevisionId = revision.Id,
                OperationId = Guid.NewGuid(),
                Status = status,
                Code = status == "passed" ? "verified" : "package-refused",
                CompletedAt = DateTime.UtcNow,
                PackageSha256 = request.DeclaredPackageSha256,
                PackageBytes = 4,
                PolicyJson = JsonSerializer.Serialize(Settings.Verifier),
                ReaderCommit = Reader,
                VerifierImage = Settings.Verifier.Image,
                ResultJson = JsonSerializer.Serialize(package)
            };
            Db.ValidationReports.Add(report); revision.CurrentValidationReportId = report.Id; revision.UploadedAt = DateTime.UtcNow;
            revision.PackageVerifiedAt = status == "passed" ? DateTime.UtcNow : null; submission.Status = "in-review";
            Db.Namespaces.Add(new() { TenantId = Tenant, ChannelKey = "arke-worlds", WorldId = request.WorldId, CreatorPartyId = CreatorParty });
            await Db.SaveChangesAsync(); AsEditor();
            return ((await Creators.GetAsync(receipt.Id)) ?? receipt, request);
        }
        public static EditorialDecisionRequest Decision(EditorialReviewDetail detail, string kind = "approve") => new(Guid.NewGuid(), detail.Submission.Revision.Id,
            detail.Snapshot!.Digest, detail.Submission.Version, kind, "Private editorial rationale", "Ready for the next step", new(true, true, true));
        public void Dispose() => Db.Dispose();
    }

    [Fact]
    public async Task Approval_Should_BindImmutablePresentationAndEnterApprovedHold()
    {
        using var h = new Harness(); var (receipt, _) = await h.SeedAsync();
        var detail = await h.Reviews.GetAsync(receipt.Id);
        detail.BlockingCode.Should().BeNull(); detail.ApprovalBlockingCode.Should().BeNull();
        detail.Snapshot!.MaterialJson.Should().Contain("first-story").And.Contain("private-contract-001").And.Contain("Audience").And.Contain("ManifestSha256");
        var request = Harness.Decision(detail); var decided = await h.Reviews.DecideAsync(receipt.Id, request);
        decided.ActorUserId.Should().Be(h.EditorUser); decided.IsActive.Should().BeTrue();
        (await h.Db.Submissions.SingleAsync()).Status.Should().Be("approved");
        (await h.Db.Submissions.SingleAsync()).ActiveEditorialDecisionId.Should().Be(decided.Id);
        (await h.Reviews.DecideAsync(receipt.Id, request)).Id.Should().Be(decided.Id);
        (await h.Db.EditorialDecisions.CountAsync()).Should().Be(1);
        (await h.Db.ReviewSnapshots.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("request-changes", "changes-requested")]
    [InlineData("reject", "rejected")]
    public async Task DecisionAndReplacement_Should_KeepHistoryAndRequireFreshVerification(string kind, string status)
    {
        using var h = new Harness(); var (receipt, original) = await h.SeedAsync(); var detail = await h.Reviews.GetAsync(receipt.Id);
        var decision = Harness.Decision(detail, kind); await h.Reviews.DecideAsync(receipt.Id, decision);
        (await h.Db.Submissions.SingleAsync()).Status.Should().Be(status);
        h.AsCreator(); var history = await h.Revisions.DecisionsAsync(receipt.Id);
        JsonSerializer.Serialize(history).Should().NotContain("Private editorial rationale").And.NotContain("ActorUserId");
        history.Items.Single().Message.Should().Be(decision.CreatorMessage);
        var current = (await h.Creators.GetAsync(receipt.Id))!; var operation = Guid.NewGuid();
        var changed = original with { ClientRequestId = operation, Title = "Revised public title" };
        var command = new ReplaceSubmissionRevisionRequest(operation, current.Revision.Id, current.Version, changed);
        var replacement = await h.Revisions.ReplaceAsync(receipt.Id, command);
        replacement.Revision.Number.Should().Be(2); replacement.Revision.PackageVerified.Should().BeFalse(); replacement.Status.Should().Be("awaiting-upload");
        (await h.Db.Submissions.SingleAsync()).ActiveEditorialDecisionId.Should().BeNull();
        (await h.Revisions.ReplaceAsync(receipt.Id, command)).Revision.Id.Should().Be(replacement.Revision.Id);
        (await h.Creators.CreateAsync(original)).Submission.Revision.Id.Should().Be(replacement.Revision.Id);
        (await h.Db.ValidationReports.CountAsync()).Should().Be(1); (await h.Db.EditorialDecisions.CountAsync()).Should().Be(1);
        h.AsEditor(); (await h.Reviews.DecideAsync(receipt.Id, decision)).IsActive.Should().BeFalse();
        (await h.Reviews.GetAsync(receipt.Id)).BlockingCode.Should().Be("passing-validation-required");
    }

    [Theory]
    [InlineData("origin-user")]
    [InlineData("creator-party")]
    [InlineData("non-primary-party")]
    public async Task SelfReview_Should_BeDeniedEvenWithPermissionAndChannelAssignment(string mode)
    {
        using var h = new Harness(); var (receipt, _) = await h.SeedAsync();
        if (mode == "origin-user") h.Actor.UserId = h.CreatorUser;
        if (mode == "creator-party") h.Party = h.CreatorParty;
        if (mode == "non-primary-party") h.LinkedParties = [h.EditorParty, h.CreatorParty];
        var read = () => h.Reviews.GetAsync(receipt.Id);
        (await read.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("self-review-forbidden");
        (await h.Reviews.ListAsync("arke-worlds")).Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("permission", 403)]
    [InlineData("tenant", 403)]
    [InlineData("channel", 404)]
    [InlineData("review-disabled", 404)]
    [InlineData("no-party", 403)]
    public async Task Authority_Should_FailClosed(string missing, int status)
    {
        using var h = new Harness(); var (receipt, _) = await h.SeedAsync();
        if (missing == "anonymous") h.Actor.IsAuthenticated = false;
        if (missing == "permission") h.CanReview = false;
        if (missing == "tenant") h.Actor.TenantId = Guid.NewGuid();
        if (missing == "channel") h.Settings.Channels["arke-worlds"].ReviewerUserIds.Clear();
        if (missing == "review-disabled") h.Settings.Channels["arke-worlds"].ReviewEnabled = false;
        if (missing == "no-party") h.Party = Guid.Empty;
        var read = () => h.Reviews.GetAsync(receipt.Id);
        (await read.Should().ThrowAsync<PublishingException>()).Which.Status.Should().Be(status);
        (await h.Db.EditorialDecisions.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("held")]
    [InlineData("pending")]
    public async Task IncompleteValidation_Should_NeverCreateDecisionAuthority(string validation)
    {
        using var h = new Harness(); var (receipt, _) = await h.SeedAsync(validation);
        var detail = await h.Reviews.GetAsync(receipt.Id); detail.Snapshot.Should().BeNull(); detail.BlockingCode.Should().Be("passing-validation-required");
        var forged = new EditorialDecisionRequest(Guid.NewGuid(), receipt.Revision.Id, new string('a', 64), detail.Submission.Version, "approve", "Reason", "", new(true, true, true));
        var decide = () => h.Reviews.DecideAsync(receipt.Id, forged);
        (await decide.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("passing-validation-required");
    }

    [Fact]
    public async Task LegacyVerifierReport_Should_RequireFreshPresentationEvidence()
    {
        using var h = new Harness(); var (receipt, _) = await h.SeedAsync(presentation: false);
        (await h.Reviews.GetAsync(receipt.Id)).BlockingCode.Should().Be("review-presentation-unavailable");
    }

    [Theory]
    [InlineData("version", "review-version-changed")]
    [InlineData("digest", "review-material-changed")]
    [InlineData("revision", "review-revision-changed")]
    [InlineData("policy", "review-material-changed")]
    [InlineData("verifier", "review-policy-changed")]
    public async Task ChangedReviewContext_Should_RejectWithoutAdvancing(string change, string code)
    {
        using var h = new Harness(); var (receipt, _) = await h.SeedAsync(); var detail = await h.Reviews.GetAsync(receipt.Id); var request = Harness.Decision(detail);
        if (change == "version") request = request with { ExpectedVersion = Convert.ToBase64String(new byte[8]) };
        if (change == "digest") request = request with { ReviewDigest = new string('0', 64) };
        if (change == "revision") request = request with { RevisionId = Guid.NewGuid() };
        if (change == "policy") h.Settings.Channels["arke-worlds"].ReviewPolicyVersion = "editorial-v2";
        if (change == "verifier") h.Settings.Verifier.MaxAssets--;
        var decide = () => h.Reviews.DecideAsync(receipt.Id, request);
        (await decide.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be(code);
        (await h.Db.EditorialDecisions.CountAsync()).Should().Be(0); (await h.Db.Submissions.SingleAsync()).Status.Should().Be("in-review");
    }

    [Theory]
    [InlineData("evidence", "rights-evidence-required")]
    [InlineData("coverage", "rights-asset-coverage-required")]
    [InlineData("artwork", "artwork-evidence-required")]
    [InlineData("checks", "editorial-checks-required")]
    public async Task Approval_Should_RequireRightsArtworkAndHumanChecks(string missing, string code)
    {
        using var h = new Harness(); var material = h.Declaration().EditorialMaterial!;
        if (missing == "coverage") material = material with { RightsEvidence = [] };
        if (missing == "artwork") material = material with { ArtworkAssetId = "missing" };
        var (receipt, _) = await h.SeedAsync(material: material, omitMaterial: missing == "evidence");
        var detail = await h.Reviews.GetAsync(receipt.Id); var request = Harness.Decision(detail);
        if (missing == "checks") request = request with { Checks = null };
        var decide = () => h.Reviews.DecideAsync(receipt.Id, request);
        (await decide.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be(code);
        // Missing evidence can be returned to the creator without pretending it passed approval checks.
        (await h.Reviews.DecideAsync(receipt.Id, request with { Decision = "request-changes" })).Decision.Should().Be("request-changes");
    }

    [Fact]
    public async Task ReusedDecisionOperation_Should_RejectChangedArguments()
    {
        using var h = new Harness(); var (receipt, _) = await h.SeedAsync(); var request = Harness.Decision(await h.Reviews.GetAsync(receipt.Id));
        await h.Reviews.DecideAsync(receipt.Id, request);
        var conflict = () => h.Reviews.DecideAsync(receipt.Id, request with { CreatorMessage = "Changed message" });
        (await conflict.Should().ThrowAsync<PublishingException>()).Which.Code.Should().Be("decision-operation-conflict");
    }

    [Theory]
    [InlineData("decision")]
    [InlineData("snapshot")]
    [InlineData("validation")]
    [InlineData("declaration")]
    [InlineData("delete-decision")]
    public async Task Persistence_Should_RejectEditsToHistoricalEvidence(string record)
    {
        using var h = new Harness(); var (receipt, _) = await h.SeedAsync(); await h.Reviews.DecideAsync(receipt.Id, Harness.Decision(await h.Reviews.GetAsync(receipt.Id)));
        if (record == "decision") (await h.Db.EditorialDecisions.SingleAsync()).InternalReason = "Overwrite";
        if (record == "snapshot") (await h.Db.ReviewSnapshots.SingleAsync()).MaterialJson = "Overwrite";
        if (record == "delete-decision") h.Db.EditorialDecisions.Remove(await h.Db.EditorialDecisions.SingleAsync());
        if (record == "validation") (await h.Db.ValidationReports.SingleAsync()).Status = "held";
        if (record == "declaration") (await h.Db.Revisions.SingleAsync()).Title = "Secret edit";
        var write = () => h.Db.SaveChangesAsync(); await write.Should().ThrowAsync<InvalidOperationException>();
    }
}
