using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;

using Aonik.IntegrationTests.Support;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public class PublishingSubmissionSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private static CreateSubmissionRequest Request() => new(Guid.NewGuid(), "arke-worlds", "The Lantern Garden",
        "Example Creator", "An illustrated edition.", "en", "lantern-garden", "2026-11-03",
        "lantern-garden.zip", 1024, new string('a', 64), "test-v1", true);

    private sealed class AdmissionBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _both.TrySetResult();
            await _both.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

    private PublishingDbContext Context(Guid tenantId, Guid userId, SaveChangesInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<PublishingDbContext>(database.CreateOptions<PublishingDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new PublishingDbContext(options.Options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId));
    }

    private static CreatorSubmissionService Service(PublishingDbContext context, Guid tenantId, Guid userId, Guid partyId)
    {
        var actor = new TestCurrentUserContext
        {
            UserId = userId, TenantId = tenantId, IsAuthenticated = true, Roles = ["PersonalUser"],
        };
        var parties = new Mock<ICurrentPartyResolver>();
        parties.Setup(x => x.GetCurrentPartyIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(partyId);
        var settings = new PublishingOptions { Channels = { ["arke-worlds"] = new PublishingChannelOptions
        {
            TenantId = tenantId, Enabled = true, MaxPackageBytes = 100_000_000, RightsDeclarationVersion = "test-v1",
        } } };
        return new CreatorSubmissionService(context, new TestTenantProvider(tenantId), actor, parties.Object,
            Mock.Of<IPermissionService>(), Mock.Of<IModuleGate>(), Options.Create(settings));
    }

    [SkippableFact]
    public async Task ConcurrentCreate_Should_ReturnOneReceiptAndOneRevision()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var party = Guid.NewGuid();
        var request = Request();
        var barrier = new AdmissionBarrier();
        await using var first = Context(tenant, user, barrier);
        await using var second = Context(tenant, user, barrier);

        var results = await Task.WhenAll(Service(first, tenant, user, party).CreateAsync(request),
            Service(second, tenant, user, party).CreateAsync(request));

        results.Should().ContainSingle(x => x.Inserted);
        results.Select(x => x.Submission.Id).Distinct().Should().ContainSingle();
        await using var verify = Context(tenant, user);
        (await verify.Submissions.CountAsync()).Should().Be(1);
        (await verify.Revisions.CountAsync()).Should().Be(1);
        (await verify.Submissions.SingleAsync()).RowVersion.Should().HaveCount(8);
    }

    [SkippableFact]
    public async Task ConcurrentChangedRequest_Should_ConflictWithoutOrphanRevision()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var party = Guid.NewGuid();
        var request = Request();
        var barrier = new AdmissionBarrier();
        await using var first = Context(tenant, user, barrier);
        await using var second = Context(tenant, user, barrier);
        async Task<string> Attempt(CreatorSubmissionService service, CreateSubmissionRequest input)
        {
            try { await service.CreateAsync(input); return "created"; }
            catch (PublishingException ex) when (ex.Code == "request-id-reused") { return "conflict"; }
        }

        var outcomes = await Task.WhenAll(Attempt(Service(first, tenant, user, party), request),
            Attempt(Service(second, tenant, user, party), request with { Title = "Changed" }));

        outcomes.Should().BeEquivalentTo("created", "conflict");
        await using var verify = Context(tenant, user);
        (await verify.Submissions.CountAsync()).Should().Be(1);
        (await verify.Revisions.CountAsync()).Should().Be(1);
    }

    [SkippableFact]
    public async Task FailedRevisionInsert_Should_RollBackSubmissionInsert()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
        await using var context = Context(tenant, user);
        var submission = new PublicationSubmission
        {
            TenantId = tenant, CreatorPartyId = Guid.NewGuid(), OriginatingUserId = user,
            ClientRequestId = Guid.NewGuid(), ChannelKey = "arke-worlds", CurrentRevisionId = Guid.NewGuid(),
        };
        context.Submissions.Add(submission);
        context.Revisions.Add(new SubmissionRevision
        {
            Id = submission.CurrentRevisionId, TenantId = tenant, SubmissionId = submission.Id,
            Number = 1, Title = new string('x', 201),
        });

        var save = () => context.SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateException>();

        await using var verify = Context(tenant, user);
        (await verify.Submissions.CountAsync()).Should().Be(0);
        (await verify.Revisions.CountAsync()).Should().Be(0);
    }

    [SkippableFact]
    public async Task CreatorReads_Should_HideOtherTenantEvenWithSamePartyAndSubmissionId()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var party = Guid.NewGuid();
        await using var owner = Context(tenant, user);
        var receipt = await Service(owner, tenant, user, party).CreateAsync(Request());
        var foreignTenant = Guid.NewGuid();
        await using var foreign = Context(foreignTenant, user);
        var foreignService = Service(foreign, foreignTenant, user, party);

        (await foreignService.GetAsync(receipt.Submission.Id)).Should().BeNull();
        (await foreignService.ListAsync()).Items.Should().BeEmpty();
        (await foreign.Submissions.CountAsync()).Should().Be(0, "the module tenant filter is also active");
    }
}
