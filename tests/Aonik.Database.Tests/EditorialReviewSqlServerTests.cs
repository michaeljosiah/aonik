using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public class EditorialReviewSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private sealed class Scope : IDisposable
    {
        public readonly PublishingDbContext Db;
        public readonly CreatorSubmissionService Creators;
        public readonly EditorialReviewService Reviews;
        public readonly SubmissionRevisionService Revisions;
        public Scope(SqlLocalDbFixture database, Guid tenant, Guid user, Guid party, PublishingOptions settings, SaveChangesInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<PublishingDbContext>(database.CreateOptions<PublishingDbContext>());
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            var tenantProvider = new TestTenantProvider(tenant);
            Db = new(builder.Options, tenantProvider, new TestCurrentUserProvider(user));
            var actor = new TestCurrentUserContext { UserId = user, TenantId = tenant, IsAuthenticated = true, Roles = ["PersonalUser", "Operations"] };
            var parties = new Mock<ICurrentPartyResolver>(); parties.Setup(p => p.GetCurrentPartyIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(party);
            var memberships = new Mock<IUserPartyMembershipReader>(); memberships.Setup(m => m.GetPartyIdsAsync(tenant, user, It.IsAny<CancellationToken>())).ReturnsAsync(new Guid[] { party });
            var permissions = new Mock<IPermissionService>(); permissions.Setup(p => p.HasPermissionAsync(user, "Publishing.Review", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var config = Microsoft.Extensions.Options.Options.Create(settings);
            Creators = new(Db, tenantProvider, actor, parties.Object, permissions.Object, Mock.Of<IModuleGate>(), config);
            Reviews = new(Db, new EditorialReviewAuthorization(tenantProvider, actor, parties.Object, memberships.Object, permissions.Object, Mock.Of<IModuleGate>(), config),
                Mock.Of<IPublishingObjectStore>(), config, Mock.Of<IClock>(c => c.UtcNow == DateTime.UtcNow));
            Revisions = new(Db, Creators, config);
        }
        public void Dispose() => Db.Dispose();
    }
    private sealed record Setup(Guid Tenant, Guid CreatorUser, Guid CreatorParty, Guid EditorUser, Guid EditorParty,
        PublishingOptions Settings, SubmissionReceipt Receipt, CreateSubmissionRequest Declaration);
    private async Task<Setup> Seed()
    {
        var tenant = Guid.NewGuid(); var creator = Guid.NewGuid(); var creatorParty = Guid.NewGuid(); var editor = Guid.NewGuid(); var editorParty = Guid.NewGuid();
        var settings = new PublishingOptions
        {
            Verifier = new() { ReaderCommit = "27df122566bcdef18126932c15a74112e18b8573", Image = "test/verifier@sha256:" + new string('a', 64) },
            Channels ={ ["arke-worlds"]=new(){TenantId=tenant,Enabled=true,NamespaceAdmissionEnabled=true,MaxPackageBytes=1000,RightsDeclarationVersion="test-v1",
                ReviewEnabled=true,ReviewPolicyVersion="editorial-v1",ReviewerUserIds=[editor]} }
        };
        using var scope = new Scope(database, tenant, creator, creatorParty, settings);
        var declaration = new CreateSubmissionRequest(Guid.NewGuid(), "arke-worlds", "Lantern Garden", "Creator", "Edition", "en", "world-" + Guid.NewGuid().ToString("N"), "edition-one",
            "edition.zip", 4, new string('b', 64), "test-v1", true, new("children", "Ages 7+", "world", "cover", "Original creator", [new("Original work", "contract-001", ["cover"])]));
        var receipt = (await scope.Creators.CreateAsync(declaration)).Submission;
        var revision = await scope.Db.Revisions.SingleAsync(); var submission = await scope.Db.Submissions.SingleAsync();
        var asset = new PackageAsset("cover", "cover.png", "image/png", 4, new string('c', 64));
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("edition-one\ncover.png " + asset.Sha256)));
        var manifest = JsonSerializer.SerializeToElement(new
        {
            format = "arke-world-package",
            schemaVersion = 1,
            id = declaration.WorldId,
            edition = declaration.Edition,
            name = declaration.Title,
            productions = new[] { new { id = "story-one", title = "Story", picture = "cover" } },
            assets = new { cover = new { path = asset.Path, sha256 = asset.Sha256 } }
        });
        var package = new PackageVerificationResult(true, "verified", settings.Verifier.ReaderCommit, declaration.DeclaredPackageSha256, 4, declaration.WorldId,
            declaration.Edition, fingerprint, "world", null, null, "cover", [asset], new(1, new string('d', 64), manifest));
        var report = new PublicationValidationReport
        {
            TenantId = tenant,
            SubmissionId = receipt.Id,
            RevisionId = revision.Id,
            OperationId = Guid.NewGuid(),
            Status = "passed",
            Code = "verified",
            CompletedAt = DateTime.UtcNow,
            PackageSha256 = declaration.DeclaredPackageSha256,
            PackageBytes = 4,
            PolicyJson = JsonSerializer.Serialize(settings.Verifier),
            ReaderCommit = settings.Verifier.ReaderCommit,
            VerifierImage = settings.Verifier.Image,
            ResultJson = JsonSerializer.Serialize(package)
        };
        scope.Db.ValidationReports.Add(report); revision.CurrentValidationReportId = report.Id; revision.PackageVerifiedAt = DateTime.UtcNow; revision.UploadedAt = DateTime.UtcNow; submission.Status = "in-review";
        scope.Db.Namespaces.Add(new() { TenantId = tenant, ChannelKey = "arke-worlds", WorldId = declaration.WorldId, CreatorPartyId = creatorParty });
        await scope.Db.SaveChangesAsync(); return new(tenant, creator, creatorParty, editor, editorParty, settings, receipt, declaration);
    }
    private Scope Editor(Setup s, SaveChangesInterceptor? interceptor = null) => new(database, s.Tenant, s.EditorUser, s.EditorParty, s.Settings, interceptor);
    private static EditorialDecisionRequest Command(EditorialReviewDetail detail) => new(Guid.NewGuid(), detail.Submission.Revision.Id, detail.Snapshot!.Digest,
        detail.Submission.Version, "approve", "Private editorial rationale", "Your edition is approved", new(true, true, true));
    private static async Task<string> Outcome(Func<Task> action)
    {
        try { await action(); return "accepted"; } catch (PublishingException ex) { return ex.Code; }
    }

    [SkippableFact]
    public async Task DuplicateConcurrentDecisions_Should_CommitOneDecisionAndReplayTheWinner()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason); var s = await Seed();
        using var read = Editor(s); var command = Command(await read.Reviews.GetAsync(s.Receipt.Id));
        using var a = Editor(s); using var b = Editor(s);
        var results = await Task.WhenAll(a.Reviews.DecideAsync(s.Receipt.Id, command), b.Reviews.DecideAsync(s.Receipt.Id, command));
        results.Select(r => r.Id).Distinct().Should().ContainSingle();
        using var verify = Editor(s); (await verify.Db.EditorialDecisions.CountAsync()).Should().Be(1);
        (await verify.Db.Submissions.SingleAsync()).Status.Should().Be("approved");
        (await verify.Db.Submissions.SingleAsync()).RowVersion.Should().NotEqual(Convert.FromBase64String(command.ExpectedVersion));
    }

    [SkippableFact]
    public async Task ChangedDuplicateOperation_Should_ConflictWithoutAddingHistory()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason); var s = await Seed(); using var read = Editor(s); var command = Command(await read.Reviews.GetAsync(s.Receipt.Id));
        using var a = Editor(s); using var b = Editor(s);
        var outcomes = await Task.WhenAll(Outcome(async () => { await a.Reviews.DecideAsync(s.Receipt.Id, command); }),
            Outcome(async () => { await b.Reviews.DecideAsync(s.Receipt.Id, command with { InternalReason = "Different decision material" }); }));
        outcomes.Should().BeEquivalentTo("accepted", "decision-operation-conflict");
        using var verify = Editor(s); (await verify.Db.EditorialDecisions.CountAsync()).Should().Be(1);
    }

    [SkippableFact]
    public async Task CompetingEditors_Should_AllowOnlyOneExpectedRowVersion()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason); var s = await Seed(); using var read = Editor(s); var command = Command(await read.Reviews.GetAsync(s.Receipt.Id));
        var otherUser = Guid.NewGuid(); s.Settings.Channels["arke-worlds"].ReviewerUserIds.Add(otherUser);
        using var a = Editor(s); using var b = new Scope(database, s.Tenant, otherUser, Guid.NewGuid(), s.Settings);
        var outcomes = await Task.WhenAll(Outcome(async () => { await a.Reviews.DecideAsync(s.Receipt.Id, command); }),
            Outcome(async () => { await b.Reviews.DecideAsync(s.Receipt.Id, command with { OperationId = Guid.NewGuid(), Decision = "reject" }); }));
        outcomes.Should().BeEquivalentTo("accepted", "review-version-changed");
        using var verify = Editor(s); (await verify.Db.EditorialDecisions.CountAsync()).Should().Be(1);
    }

    [SkippableFact]
    public async Task ReplacementRacingApproval_Should_NeverCarryApprovalToDifferentMaterial()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason); var s = await Seed(); using var read = Editor(s); var command = Command(await read.Reviews.GetAsync(s.Receipt.Id));
        var op = Guid.NewGuid(); var replacement = new ReplaceSubmissionRevisionRequest(op, s.Receipt.Revision.Id, command.ExpectedVersion, s.Declaration with { ClientRequestId = op, Title = "Changed public title" });
        using var editor = Editor(s); using var creator = new Scope(database, s.Tenant, s.CreatorUser, s.CreatorParty, s.Settings);
        var outcomes = await Task.WhenAll(Outcome(async () => { await editor.Reviews.DecideAsync(s.Receipt.Id, command); }),
            Outcome(async () => { await creator.Revisions.ReplaceAsync(s.Receipt.Id, replacement); }));
        outcomes.Should().BeEquivalentTo("accepted", "review-version-changed");
        using var verify = Editor(s); var submission = await verify.Db.Submissions.SingleAsync();
        if (submission.Status == "approved")
        {
            submission.CurrentRevisionId.Should().Be(s.Receipt.Revision.Id); submission.ActiveEditorialDecisionId.Should().NotBeNull();
            (await verify.Db.Revisions.CountAsync()).Should().Be(1);
        }
        else
        {
            submission.Status.Should().Be("awaiting-upload"); submission.ActiveEditorialDecisionId.Should().BeNull();
            (await verify.Db.EditorialDecisions.CountAsync()).Should().Be(0);
        }
    }

    private sealed class InvalidDecision : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            foreach (var entry in eventData.Context!.ChangeTracker.Entries<EditorialDecision>().Where(e => e.State == EntityState.Added))
                entry.Entity.PolicyVersion = new string('x', 121); // Inject a real SQL column-constraint failure after service checks.
            return ValueTask.FromResult(result);
        }
    }

    [SkippableFact]
    public async Task FailedDecisionInsert_Should_RollBackApprovalAndPreserveReviewEvidence()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason); var s = await Seed(); using var read = Editor(s); var command = Command(await read.Reviews.GetAsync(s.Receipt.Id));
        using var failing = Editor(s, new InvalidDecision()); var save = () => failing.Reviews.DecideAsync(s.Receipt.Id, command);
        await save.Should().ThrowAsync<DbUpdateException>();
        using var verify = Editor(s); var submission = await verify.Db.Submissions.SingleAsync(); submission.Status.Should().Be("in-review"); submission.ActiveEditorialDecisionId.Should().BeNull();
        Convert.ToBase64String(submission.RowVersion).Should().Be(command.ExpectedVersion);
        (await verify.Db.EditorialDecisions.CountAsync()).Should().Be(0); (await verify.Db.ReviewSnapshots.CountAsync()).Should().Be(1);
    }

    [SkippableFact]
    public async Task CanonicalContext_Should_RejectAuditRewriteAndDeletion()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason); var s = await Seed(); using var editor = Editor(s);
        await editor.Reviews.DecideAsync(s.Receipt.Id, Command(await editor.Reviews.GetAsync(s.Receipt.Id)));
        using (var canonical = new AonikDbContext(database.CreateOptions<AonikDbContext>(), new TestTenantProvider(s.Tenant)))
        {
            (await canonical.EditorialDecisions.SingleAsync(d => d.TenantId == s.Tenant)).CreatorMessage = "Rewrite";
            var save = () => canonical.SaveChangesAsync(); await save.Should().ThrowAsync<InvalidOperationException>();
        }
        using (var canonical = new AonikDbContext(database.CreateOptions<AonikDbContext>(), new TestTenantProvider(s.Tenant)))
        {
            canonical.EditorialDecisions.Remove(await canonical.EditorialDecisions.SingleAsync(d => d.TenantId == s.Tenant));
            Action save = () => canonical.SaveChanges(); save.Should().Throw<InvalidOperationException>();
        }
        using (var canonical = new AonikDbContext(database.CreateOptions<AonikDbContext>(), new TestTenantProvider(s.Tenant)))
        {
            (await canonical.EditorialReviewSnapshots.SingleAsync(d => d.TenantId == s.Tenant)).MaterialJson = "Rewrite through explicit async overload";
            var save = () => canonical.SaveChangesAsync(true, CancellationToken.None);
            await save.Should().ThrowAsync<InvalidOperationException>();
        }
        using var verify = Editor(s); (await verify.Db.EditorialDecisions.SingleAsync()).CreatorMessage.Should().Be("Your edition is approved");
    }
}
