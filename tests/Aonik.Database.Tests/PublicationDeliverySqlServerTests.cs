using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Aonik.IntegrationTests.Support;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Testing;

namespace Aonik.Database.Tests;

public class PublicationDeliverySqlServerTests(SqlLocalDbFixture database):IClassFixture<SqlLocalDbFixture>
{
    private PublicationScenario Scope(PublicationScenario? parent=null,SaveChangesInterceptor? interceptor=null)
    {
        var options=new DbContextOptionsBuilder<PublishingDbContext>(database.CreateOptions<PublishingDbContext>());
        if(interceptor is not null)options.AddInterceptors(interceptor);
        return new(options.Options,parent);
    }
    [Fact]
    public async Task PartWithdrawal_Should_CommitCascadeReceipts_AndRepairLegacyReplayInSql()
    {
        using var h = Scope();
        await h.InitialiseAsync();
        var baseReceipt = await h.ApproveAsync("listening");
        var first = await h.Service.PublishAsync(baseReceipt.Id, await h.RequestAsync(baseReceipt.Id));
        await h.DeliverAsync(first.Id);
        var part = await h.ApproveAsync("part");
        var next = await h.Service.PublishAsync(part.Id, await h.RequestAsync(part.Id));
        await h.DeliverAsync(next.Id);
        var edition = (await h.Service.CatalogueAsync("arke-worlds")).Editions.Single(row => row.Kind == "part");
        var command = new PublicationActionRequest(Guid.NewGuid(), edition.Version, "Withdraw the synthetic sequence");

        var operation = await h.Service.WithdrawAsync(edition.Id, command);
        await h.DeliverAsync(operation.Id);

        var head = await h.HeadAsync();
        head.Should().Contain("\"serial\":55");
        (await h.Db.Submissions.Where(row => row.TenantId == h.Tenant && (row.Id == baseReceipt.Id || row.Id == part.Id)).ToListAsync())
            .Should().OnlyContain(row => row.Status == SubmissionStatuses.Withdrawn);
        (await h.Db.Submissions.SingleAsync(row => row.TenantId == h.Tenant && row.Id == baseReceipt.Id)).Status = SubmissionStatuses.Published;
        await h.Db.SaveChangesAsync();

        using var replay = Scope(h);
        (await replay.Service.WithdrawAsync(edition.Id, command)).Id.Should().Be(operation.Id);
        (await replay.Db.Submissions.AsNoTracking().SingleAsync(row => row.TenantId == h.Tenant && row.Id == baseReceipt.Id)).Status
            .Should().Be(SubmissionStatuses.Withdrawn);
        (await replay.HeadAsync()).Should().Be(head);
        (await replay.Db.PublicationOperations.CountAsync(row => row.TenantId == h.Tenant && row.Kind == "withdraw")).Should().Be(1);
    }
    [Fact]
    public async Task SqlCommitFailure_Should_RollBackCatalogueEditionAndOperation_ThenRecoverOnce()
    {
        var failure=new LoseCommit();using var h=Scope(interceptor:failure);await h.InitialiseAsync();var r=await h.ApproveAsync();var op=await h.Service.PublishAsync(r.Id,await h.RequestAsync(r.Id));
        failure.Enabled=true;await h.Invoking(x=>x.DeliverAsync(op.Id)).Should().ThrowAsync<IOException>();
        (await h.HeadAsync()).Should().Contain("\"serial\":52");
        (await h.Db.PublishedEditions.CountAsync(e=>e.TenantId==h.Tenant)).Should().Be(0);
        (await h.Db.PublicationOperations.SingleAsync(o=>o.TenantId==h.Tenant&&o.Id==op.Id)).Status.Should().Be("uncertain");
        (await h.Db.PublicationArtifacts.Where(a=>a.TenantId==h.Tenant).ToListAsync()).Should().OnlyContain(a=>a.PublicVerifiedAt!=null);
        await h.DeliverAsync(op.Id);(await h.HeadAsync()).Should().Contain("\"serial\":53");
        (await h.Db.PublishedEditions.CountAsync(e=>e.TenantId==h.Tenant)).Should().Be(1);
    }
    [Fact]
    public async Task TwoWorkers_Should_CommitUnrelatedWorldsWithoutLosingEitherHead()
    {
        using var h=Scope();await h.InitialiseAsync();var a=await h.ApproveAsync("world");var b=await h.ApproveAsync("story");
        var one=await h.Service.PublishAsync(a.Id,await h.RequestAsync(a.Id));var two=await h.Service.PublishAsync(b.Id,await h.RequestAsync(b.Id));
        using var workerOne=Scope(h);using var workerTwo=Scope(h);
        // Serializable deadlocks are legitimate retry outcomes; durable commands retain their identity.
        foreach(var result in await Task.WhenAll(Attempt(workerOne,one.Id),Attempt(workerTwo,two.Id)))_ = result;
        foreach(var id in new[]{one.Id,two.Id}){h.Clock.UtcNow=h.Clock.UtcNow.AddMinutes(11);await h.DeliverAsync(id);}
        (await h.HeadAsync()).Should().Contain(a.Revision.WorldId).And.Contain(b.Revision.WorldId);
        (await h.Db.PublicationChannels.SingleAsync(c=>c.TenantId==h.Tenant)).Serial.Should().Be(54);
        (await h.Db.CatalogueSnapshots.CountAsync(s=>s.TenantId==h.Tenant)).Should().Be(3);
    }
    [Fact]
    public async Task RepeatedDelivery_Should_KeepOneCurrentEditionAndStableSnapshot()
    {
        using var h=Scope();await h.InitialiseAsync();var r=await h.ApproveAsync();var request=await h.RequestAsync(r.Id);var op=await h.Service.PublishAsync(r.Id,request);await h.DeliverAsync(op.Id);
        using var repeat=Scope(h);await repeat.DeliverAsync(op.Id);(await repeat.Service.PublishAsync(r.Id,request)).Id.Should().Be(op.Id);
        (await h.HeadAsync()).Should().Contain("\"serial\":53");
        (await h.Db.PublishedEditions.CountAsync(e=>e.TenantId==h.Tenant&&e.IsCurrent)).Should().Be(1);
    }
    [Fact]
    public async Task StaleClaim_Should_NotCompleteAfterNewWorkerTakesOver()
    {
        using var h=Scope();await h.InitialiseAsync();var r=await h.ApproveAsync();var op=await h.Service.PublishAsync(r.Id,await h.RequestAsync(r.Id));
        var once=false;using var replacement=Scope(h);
        h.Store.BeforeVerify=async _=>{if(once)return;once=true;h.Clock.UtcNow=h.Clock.UtcNow.AddMinutes(11);await replacement.DeliverAsync(op.Id);};
        await h.DeliverAsync(op.Id);(await h.HeadAsync()).Should().Contain("\"serial\":53");
        (await h.Db.PublicationOperations.SingleAsync(o=>o.TenantId==h.Tenant&&o.Id==op.Id)).Status.Should().Be("committed");
        (await h.Db.CatalogueSnapshots.CountAsync(s=>s.TenantId==h.Tenant)).Should().Be(2);
    }
    private static async Task<bool> Attempt(PublicationScenario h,Guid id){try{await h.DeliverAsync(id);return true;}catch(Exception e)when(e is IOException or DbUpdateException or Microsoft.Data.SqlClient.SqlException){return false;}}
    private sealed class LoseCommit:SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,int result,CancellationToken cancellationToken=default)
        {
            if(Enabled&&eventData.Context!.ChangeTracker.Entries<PublicationCatalogueSnapshot>().Any(e=>e.Entity.Serial==53))
            {Enabled=false;throw new IOException("Injected failure after SQL SaveChanges but before transaction commit");}
            return ValueTask.FromResult(result);
        }
    }
}
