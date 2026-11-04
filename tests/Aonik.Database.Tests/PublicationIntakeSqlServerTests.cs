using System.Security.Cryptography;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

using Aonik.Infrastructure.Publishing;
using Aonik.IntegrationTests.Support;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public class PublicationIntakeSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private sealed class Scope : IDisposable
    {
        public readonly PublishingDbContext Db;
        public readonly CreatorSubmissionService Creator;
        public readonly PublicationIntakeService Intake;
        public Scope(SqlLocalDbFixture database, Guid tenant, Guid user, Guid party, PublishingOptions settings)
        {
            var provider = new TestTenantProvider(tenant);
            Db = new PublishingDbContext(database.CreateOptions<PublishingDbContext>(),provider,new TestCurrentUserProvider(user));
            var actor = new TestCurrentUserContext { TenantId=tenant,UserId=user,IsAuthenticated=true,Roles=["PersonalUser"] };
            var parties = new Mock<ICurrentPartyResolver>(); parties.Setup(p=>p.GetCurrentPartyIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(party);
            Creator = new CreatorSubmissionService(Db,provider,actor,parties.Object,Mock.Of<IPermissionService>(),Mock.Of<IModuleGate>(),Options.Create(settings));
            var host = Mock.Of<IHostEnvironment>(e=>e.ContentRootPath==Path.Combine(Path.GetTempPath(),"aonik-test-host"));
            var store = new PrivatePublicationObjectStore(Options.Create(settings),host,new ConfigurationBuilder().Build());
            Intake = new PublicationIntakeService(Db,Creator,store,Options.Create(settings),Mock.Of<IClock>(c=>c.UtcNow==DateTime.UtcNow));
        }
        public void Dispose()=>Db.Dispose();
    }
    private static PublishingOptions Settings(Guid tenant,string root,long creatorBytes=1024)=>new()
    {
        Intake = new PublishingIntakeOptions { PrivateRoot=root,MaxCreatorRetainedBytes=creatorBytes,MaxTenantRetainedBytes=4096,SingleShotThresholdBytes=8,PartSizeBytes=4 },
        Channels={ ["arke-worlds"]=new PublishingChannelOptions {TenantId=tenant,Enabled=true,NamespaceAdmissionEnabled=true,MaxPackageBytes=1000,RightsDeclarationVersion="test-v1"} },
    };
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static CreateSubmissionRequest Request()=>new(Guid.NewGuid(),"arke-worlds","Lantern Garden","Creator","Edition","en",
        "world-"+Guid.NewGuid().ToString("N"),"2026-11-03","edition.zip",4,Hash([1,2,3,4]),"test-v1",true);
    private static string Root()=>Path.Combine(Path.GetTempPath(),"aonik-intake-sql-"+Guid.NewGuid().ToString("N"));
    private static void Clean(string root)
    {
        if (!Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();
        if(Directory.Exists(root))Directory.Delete(root,true);
    }

    [SkippableFact]
    public async Task CompetingBegins_Should_ReserveCreatorQuotaOnlyOnceWithoutOrphanedSessions()
    {
        Skip.IfNot(database.IsAvailable,database.SkipReason);
        var tenant=Guid.NewGuid();var user=Guid.NewGuid();var party=Guid.NewGuid();var root=Root();var settings=Settings(tenant,root,4);
        try
        {
            using var seed=new Scope(database,tenant,user,party,settings);
            var a=(await seed.Creator.CreateAsync(Request())).Submission;
            var b=(await seed.Creator.CreateAsync(Request())).Submission;
            using var first=new Scope(database,tenant,user,party,settings);using var second=new Scope(database,tenant,user,party,settings);
            async Task<string> Begin(Scope scope,Guid id)
            {
                try{await scope.Intake.BeginAsync(id);return "accepted";}
                catch(PublishingException ex){return ex.Code;}
            }
            var outcomes=await Task.WhenAll(Begin(first,a.Id),Begin(second,b.Id));
            outcomes.Should().BeEquivalentTo("accepted","upload-quota-exceeded");
            using var verify=new Scope(database,tenant,user,party,settings);
            (await verify.Db.UploadSessions.CountAsync()).Should().Be(1);
            (await verify.Db.UploadQuotas.SumAsync(q=>q.ReservedBytes)).Should().Be(8);
            (await verify.Db.Namespaces.CountAsync()).Should().Be(1,"the rejected admission must roll back its namespace too");
        }finally{Clean(root);}
    }

    [SkippableFact]
    public async Task ConcurrentSamePart_Should_ReceiveAndCountBytesOnce()
    {
        Skip.IfNot(database.IsAvailable,database.SkipReason);
        var tenant=Guid.NewGuid();var user=Guid.NewGuid();var party=Guid.NewGuid();var root=Root();var settings=Settings(tenant,root);
        try
        {
            using var seed=new Scope(database,tenant,user,party,settings);
            var draft=(await seed.Creator.CreateAsync(Request())).Submission;var upload=await seed.Intake.BeginAsync(draft.Id);
            using var first=new Scope(database,tenant,user,party,settings);using var second=new Scope(database,tenant,user,party,settings);
            await Task.WhenAll(first.Intake.PutPartAsync(draft.Id,upload.Id,1,Hash([1,2,3,4]),4,new MemoryStream([1,2,3,4])),
                second.Intake.PutPartAsync(draft.Id,upload.Id,1,Hash([1,2,3,4]),4,new MemoryStream([1,2,3,4])));
            using var verify=new Scope(database,tenant,user,party,settings);
            (await verify.Db.UploadParts.CountAsync()).Should().Be(1);
            (await verify.Db.UploadSessions.SingleAsync()).ReceivedBytes.Should().Be(4);
            Directory.GetFiles(root,"part-*",SearchOption.AllDirectories).Should().ContainSingle();
        }finally{Clean(root);}
    }

    [SkippableFact]
    public async Task ConcurrentSubmit_Should_CommitOneReportAndOneOutboxEvent()
    {
        Skip.IfNot(database.IsAvailable,database.SkipReason);
        var tenant=Guid.NewGuid();var user=Guid.NewGuid();var party=Guid.NewGuid();var root=Root();var settings=Settings(tenant,root);
        try
        {
            using var seed=new Scope(database,tenant,user,party,settings);
            var draft=(await seed.Creator.CreateAsync(Request())).Submission;var upload=await seed.Intake.BeginAsync(draft.Id);
            await seed.Intake.PutPartAsync(draft.Id,upload.Id,1,Hash([1,2,3,4]),4,new MemoryStream([1,2,3,4]));
            await seed.Intake.CompleteAsync(draft.Id,upload.Id);
            using var first=new Scope(database,tenant,user,party,settings);using var second=new Scope(database,tenant,user,party,settings);
            var request=new SubmitPackageRequest(draft.Revision.Id,Guid.NewGuid());
            var results=await Task.WhenAll(first.Intake.SubmitAsync(draft.Id,request),second.Intake.SubmitAsync(draft.Id,request));
            results.Select(r=>r.ReportId).Distinct().Should().ContainSingle();
            using var verify=new Scope(database,tenant,user,party,settings);
            (await verify.Db.ValidationReports.CountAsync()).Should().Be(1);
            (await verify.Db.Set<OutboxMessage>().CountAsync(m=>m.TenantId==tenant)).Should().Be(1);
            (await verify.Db.Submissions.SingleAsync()).Status.Should().Be("validating");
        }finally{Clean(root);}
    }

    [SkippableFact]
    public async Task FailedReportInsert_Should_RollBackStatusAndOutboxTogether()
    {
        Skip.IfNot(database.IsAvailable,database.SkipReason);
        var tenant=Guid.NewGuid();var user=Guid.NewGuid();var party=Guid.NewGuid();var root=Root();var settings=Settings(tenant,root);
        try
        {
            using var seed=new Scope(database,tenant,user,party,settings);
            var draft=(await seed.Creator.CreateAsync(Request())).Submission;var upload=await seed.Intake.BeginAsync(draft.Id);
            await seed.Intake.PutPartAsync(draft.Id,upload.Id,1,Hash([1,2,3,4]),4,new MemoryStream([1,2,3,4]));await seed.Intake.CompleteAsync(draft.Id,upload.Id);
            settings.Verifier.Image=new string('x',301); // Force a SQL constraint failure inside the save unit.
            var submit=()=>seed.Intake.SubmitAsync(draft.Id,new(draft.Revision.Id,Guid.NewGuid()));
            await submit.Should().ThrowAsync<DbUpdateException>();
            using var verify=new Scope(database,tenant,user,party,settings);
            (await verify.Db.Submissions.SingleAsync()).Status.Should().Be("uploaded");
            (await verify.Db.ValidationReports.CountAsync()).Should().Be(0);
            (await verify.Db.Set<OutboxMessage>().CountAsync(m=>m.TenantId==tenant)).Should().Be(0);
        }finally{Clean(root);}
    }
}
