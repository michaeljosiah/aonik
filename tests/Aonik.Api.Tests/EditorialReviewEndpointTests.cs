using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Api.Tests;

public sealed class EditorialReviewTestFactory : CustomWebApplicationFactory
{
    public readonly Guid Tenant = Guid.NewGuid();
    public readonly string Root = Path.Combine(Path.GetTempPath(), "aonik-review-http-" + Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.Configure<PublishingOptions>(o =>
        {
            o.Channels["arke-worlds"] = new()
            {
                TenantId = Tenant,
                Enabled = true,
                NamespaceAdmissionEnabled = true,
                MaxPackageBytes = 1000,
                RightsDeclarationVersion = "test-v1",
                ReviewEnabled = true,
                ReviewPolicyVersion = "editorial-v1"
            };
            o.Intake.PrivateRoot = Root; o.Intake.MaxCreatorRetainedBytes = 1024; o.Intake.MaxTenantRetainedBytes = 10240;
            o.Verifier.ReaderCommit = "27df122566bcdef18126932c15a74112e18b8573"; o.Verifier.Image = "test/verifier@sha256:" + new string('a', 64);
        }));
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        var path = Path.GetFullPath(Root);
        if (!path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}

public class EditorialReviewEndpointTests(EditorialReviewTestFactory factory) : IClassFixture<EditorialReviewTestFactory>
{
    private PublishingOptions Settings => factory.Services.GetRequiredService<IOptions<PublishingOptions>>().Value;
    private async Task<WorkspaceTestSeeding.Person> Person(bool reviewer = true, bool assigned = true, Guid? tenant = null)
    {
        var auth = TestAuthOptions.Create().WithTenant(tenant ?? factory.Tenant).WithRoles(reviewer ? "Operations" : "PersonalUser")
            .WithPermissions(reviewer ? "Publishing.Review" : "Workspace.Use");
        var client = await factory.CreateAuthenticatedClientAsync(auth);
        var party = await WorkspaceTestSeeding.SeedPartyAsync(factory, tenant ?? factory.Tenant, auth.UserId, "Editorial test user");
        if (assigned) Settings.Channels["arke-worlds"].ReviewerUserIds.Add(auth.UserId);
        return new(auth.UserId, party, client);
    }
    private async Task<(WorkspaceTestSeeding.Person Creator, SubmissionReceipt Receipt, CreateSubmissionRequest Declaration)> Ready(string reportStatus = "passed")
    {
        var owner = await Person(reviewer: false); var bytes = new byte[] { 1, 2, 3, 4 }; var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var declaration = new CreateSubmissionRequest(Guid.NewGuid(), "arke-worlds", "Lantern Garden", "Creator", "A quiet adventure", "en", "world-" + Guid.NewGuid().ToString("N"),
            "edition-one", "edition.zip", 4, sha, "test-v1", true, new("children", "Ages 7+", "world", "cover", "Original creator", [new("Original work", "private-evidence-001", ["cover"])]));
        var created = await owner.Client.PostAsJsonAsync("/publishing/submissions", declaration); created.StatusCode.Should().Be(HttpStatusCode.Created);
        var receipt = (await created.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Submission;
        var upload = (await (await owner.Client.PostAsync($"/publishing/submissions/{receipt.Id}/uploads", null)).Content.ReadFromJsonAsync<UploadReceipt>())!;
        using var part = new HttpRequestMessage(HttpMethod.Put, $"/publishing/submissions/{receipt.Id}/uploads/{upload.Id}/parts/1");
        part.Content = new ByteArrayContent(bytes); part.Content.Headers.ContentType = new("application/octet-stream"); part.Headers.Add("X-Part-Sha256", sha);
        (await owner.Client.SendAsync(part)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.Client.PostAsync($"/publishing/submissions/{receipt.Id}/uploads/{upload.Id}/complete", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        // Trusted verifier output is a fixture in HTTP tests; the pinned Dart lane checks real package parsing separately.
        using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = factory.Tenant;
        var db = (PublishingDbContext)scope.ServiceProvider.GetRequiredService<IPublishingDataContext>();
        var revision = await db.Revisions.SingleAsync(r => r.Id == receipt.Revision.Id); var submission = await db.Submissions.SingleAsync(s => s.Id == receipt.Id);
        var asset = new PackageAsset("cover", "cover.png", "image/png", 4, new string('c', 64));
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("edition-one\ncover.png " + asset.Sha256)));
        var manifest = JsonSerializer.SerializeToElement(new
        {
            format = "arke-world-package",
            schemaVersion = 1,
            id = declaration.WorldId,
            edition = declaration.Edition,
            name = declaration.Title,
            productions = new[] { new { id = "story-one", title = "Lantern story", picture = "cover" } },
            assets = new { cover = new { path = asset.Path, sha256 = asset.Sha256 } }
        });
        var package = new PackageVerificationResult(true, "verified", Settings.Verifier.ReaderCommit, sha, 4, declaration.WorldId, declaration.Edition, fingerprint, "world", null, null, "cover", [asset], new(1, new string('d', 64), manifest));
        var report = new PublicationValidationReport
        {
            TenantId = factory.Tenant,
            SubmissionId = receipt.Id,
            RevisionId = revision.Id,
            OperationId = Guid.NewGuid(),
            Status = reportStatus,
            Code = "verified",
            CompletedAt = DateTime.UtcNow,
            PackageSha256 = sha,
            PackageBytes = 4,
            PolicyJson = JsonSerializer.Serialize(Settings.Verifier),
            ReaderCommit = Settings.Verifier.ReaderCommit,
            VerifierImage = Settings.Verifier.Image,
            ResultJson = JsonSerializer.Serialize(package)
        };
        db.ValidationReports.Add(report); revision.CurrentValidationReportId = report.Id; revision.PackageVerifiedAt = reportStatus == "passed" ? DateTime.UtcNow : null;
        submission.Status = "in-review"; await db.SaveChangesAsync(); return (owner, receipt, declaration);
    }
    private static EditorialDecisionRequest Decision(EditorialReviewDetail detail, string kind = "approve") => new(Guid.NewGuid(), detail.Submission.Revision.Id, detail.Snapshot!.Digest,
        detail.Submission.Version, kind, "Internal rights assessment", "Your edition is approved for the next step", new(true, true, true));

    [Fact]
    public async Task ReviewApproveAndReplay_Should_PersistPrivateAuditAndSafeCreatorHistory()
    {
        var (owner, receipt, _) = await Ready(); var editor = await Person();
        var queue = await editor.Client.GetFromJsonAsync<EditorialReviewPage>("/publishing/review/submissions?channelKey=arke-worlds&status=in-review");
        queue!.Items.Should().Contain(s => s.Id == receipt.Id);
        var response = await editor.Client.GetAsync($"/publishing/review/submissions/{receipt.Id}"); response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var detail = (await response.Content.ReadFromJsonAsync<EditorialReviewDetail>())!;
        var package = await editor.Client.GetAsync($"/publishing/review/submissions/{receipt.Id}/revisions/{receipt.Revision.Id}/package");
        package.StatusCode.Should().Be(HttpStatusCode.OK); package.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        var command = Decision(detail); var path = $"/publishing/review/submissions/{receipt.Id}/decisions";
        var approved = await editor.Client.PostAsJsonAsync(path, command); approved.StatusCode.Should().Be(HttpStatusCode.OK);
        var decision = (await approved.Content.ReadFromJsonAsync<EditorialDecisionResponse>())!; decision.IsActive.Should().BeTrue();
        (await (await editor.Client.PostAsJsonAsync(path, command)).Content.ReadFromJsonAsync<EditorialDecisionResponse>())!.Id.Should().Be(decision.Id);
        (await editor.Client.PostAsJsonAsync(path, command with { OperationId = Guid.NewGuid() })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var creatorHistory = await owner.Client.GetAsync($"/publishing/submissions/{receipt.Id}/decisions");
        var historyJson = await creatorHistory.Content.ReadAsStringAsync(); historyJson.Should().NotContain("Internal rights assessment").And.NotContain("actorUserId");
        (await owner.Client.GetFromJsonAsync<SubmissionReceipt>($"/publishing/submissions/{receipt.Id}"))!.Status.Should().Be("approved");
        using var scope = factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = factory.Tenant;
        var db = scope.ServiceProvider.GetRequiredService<IPublishingDataContext>();
        (await db.EditorialPackageAccesses.CountAsync(a => a.SubmissionId == receipt.Id && a.ActorUserId == editor.UserId)).Should().Be(1);
        (await db.EditorialDecisions.CountAsync(d => d.SubmissionId == receipt.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ChangesRequested_Should_AllowOwnedFreshRevisionWithoutCopyingApprovalOrValidation()
    {
        var (owner, receipt, declaration) = await Ready(); var editor = await Person();
        var detail = (await editor.Client.GetFromJsonAsync<EditorialReviewDetail>($"/publishing/review/submissions/{receipt.Id}"))!;
        (await editor.Client.PostAsJsonAsync($"/publishing/review/submissions/{receipt.Id}/decisions", Decision(detail, "request-changes"))).StatusCode.Should().Be(HttpStatusCode.OK);
        var current = (await owner.Client.GetFromJsonAsync<SubmissionReceipt>($"/publishing/submissions/{receipt.Id}"))!; var op = Guid.NewGuid();
        var command = new ReplaceSubmissionRevisionRequest(op, current.Revision.Id, current.Version, declaration with { ClientRequestId = op, Title = "Updated title" });
        var replacement = await owner.Client.PostAsJsonAsync($"/publishing/submissions/{receipt.Id}/revisions", command); replacement.StatusCode.Should().Be(HttpStatusCode.OK);
        var fresh = (await replacement.Content.ReadFromJsonAsync<SubmissionReceipt>())!; fresh.Revision.Number.Should().Be(2); fresh.Revision.PackageVerified.Should().BeFalse(); fresh.Status.Should().Be("awaiting-upload");
        (await owner.Client.PostAsJsonAsync($"/publishing/submissions/{receipt.Id}/revisions", command)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.Client.PostAsJsonAsync($"/publishing/submissions/{receipt.Id}/revisions", command with { Declaration = command.Declaration with { Title = "Conflicting" } })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReviewRoutes_Should_EnforceAuthenticationPermissionTenantAndChannel()
    {
        var (_, receipt, _) = await Ready(); var path = $"/publishing/review/submissions/{receipt.Id}";
        (await factory.CreateClient().GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await (await Person(reviewer: false)).Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await (await Person(assigned: false)).Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await (await Person(tenant: Guid.NewGuid())).Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var editor = await Person(); var req = new EditorialDecisionRequest(Guid.NewGuid(), receipt.Revision.Id, new string('0', 64), Convert.ToBase64String(new byte[8]), "approve", "Reason", "", new(true, true, true));
        (await editor.Client.PostAsJsonAsync(path + "/decisions", req)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OriginatingUserAndNonPrimaryCreatorParty_Should_BothBeDeniedReview()
    {
        var (owner, receipt, _) = await Ready(); var path = $"/publishing/review/submissions/{receipt.Id}";
        var auth = TestAuthOptions.Create().WithTenant(factory.Tenant).WithRoles("PersonalUser").WithPermissions("Publishing.Review"); auth.UserId = owner.UserId;
        var ownerAsReviewer = await factory.CreateAuthenticatedClientAsync(auth);
        (await ownerAsReviewer.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var editor = await Person();
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = factory.Tenant;
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.UserParties.Add(new UserParty { TenantId = factory.Tenant, UserId = editor.UserId, PartyId = owner.PartyId, LinkType = "Business", CreatedAt = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        }
        (await editor.Client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var queue = await editor.Client.GetFromJsonAsync<EditorialReviewPage>("/publishing/review/submissions?channelKey=arke-worlds");
        queue!.Items.Should().NotContain(s => s.Id == receipt.Id);
    }

    [Fact]
    public async Task ForgedApprovalFieldsAndMissingChecks_Should_NeverGrantApproval()
    {
        var (_, receipt, _) = await Ready(); var editor = await Person(); var path = $"/publishing/review/submissions/{receipt.Id}";
        var detail = (await editor.Client.GetFromJsonAsync<EditorialReviewDetail>(path))!; var req = Decision(detail);
        (await editor.Client.PostAsJsonAsync(path + "/decisions", new { req.OperationId, req.RevisionId, req.ReviewDigest, req.ExpectedVersion, req.Decision, req.InternalReason, req.CreatorMessage, req.Checks, approved = true, actorUserId = editor.UserId })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await editor.Client.PostAsJsonAsync(path + "/decisions", req with { Checks = null })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await editor.Client.PostAsJsonAsync(path + "/decisions", req with { ReviewDigest = new string('0', 64) })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task FailedValidation_Should_KeepReviewBlockedEvenWithForgedDigest()
    {
        var (_, receipt, _) = await Ready("held"); var editor = await Person(); var path = $"/publishing/review/submissions/{receipt.Id}";
        var detail = (await editor.Client.GetFromJsonAsync<EditorialReviewDetail>(path))!; detail.Snapshot.Should().BeNull();
        var req = new EditorialDecisionRequest(Guid.NewGuid(), receipt.Revision.Id, new string('a', 64), detail.Submission.Version, "approve", "Reason", "", new(true, true, true));
        (await editor.Client.PostAsJsonAsync(path + "/decisions", req)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
