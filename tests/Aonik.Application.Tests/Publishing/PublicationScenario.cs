using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Modules;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Publishing.Testing;

internal sealed class PublicationScenario : IDisposable
{
    public readonly Guid Tenant=Guid.NewGuid(),Creator=Guid.NewGuid(),CreatorParty=Guid.NewGuid(),Editor=Guid.NewGuid(),EditorParty=Guid.NewGuid();
    public readonly TestCurrentUserContext Actor=new();
    public readonly PublishingDbContext Db;
    public readonly PublishingOptions Settings;
    public readonly PublicationService Service;
    public readonly EditorialReviewService Reviews;
    public readonly CreatorSubmissionService Creators;
    public readonly PublicationDeliveryHandler Worker;
    public readonly MemoryPublicStore Store=new();
    public readonly Mock<IPublishingObjectStore> Private=new();
    public readonly Mock<IUserPartyMembershipReader> Memberships=new();
    public readonly Mock<IPermissionService> Permissions=new();
    public readonly MutableClock Clock=new();
    public Guid Party;
    public bool Allowed=true;
    public PublicationScenario(DbContextOptions<PublishingDbContext>? options=null,PublicationScenario? parent=null)
    {
        if(parent is not null){Tenant=parent.Tenant;Creator=parent.Creator;CreatorParty=parent.CreatorParty;Editor=parent.Editor;EditorParty=parent.EditorParty;Store=parent.Store;Clock=parent.Clock;_inputs=parent._inputs;}
        var tenants=new TestTenantProvider(Tenant);var user=new Mock<ICurrentUserProvider>();user.Setup(x=>x.GetCurrentUserId()).Returns(()=>Actor.UserId);
        Db=new(options??new DbContextOptionsBuilder<PublishingDbContext>().UseInMemoryDatabase("delivery-"+Guid.NewGuid()).Options,tenants,user.Object,Clock);
        Settings=parent?.Settings??new(){Verifier=new(){ReaderCommit="27df122566bcdef18126932c15a74112e18b8573",Image="test/verifier@sha256:"+new string('a',64)},
            Delivery=new(){PublicRoot=Path.GetTempPath()},Channels={ ["arke-worlds"]=new(){TenantId=Tenant,Enabled=true,NamespaceAdmissionEnabled=true,MaxPackageBytes=100000000,
                RightsDeclarationVersion="rights-v1",ReviewEnabled=true,ReviewPolicyVersion="review-v1",ReviewerUserIds=[Editor],PublisherUserIds=[Editor],DeliveryEnabled=true,
                PublicCatalogueEnabled=true,DeliveryPolicyVersion="delivery-v1",PublicBaseUrl="https://public.example.test",BaselineCatalogueJson="{\"version\":2,\"serial\":52,\"worlds\":[]}",BaselineEvidenceReference="qualification/test-baseline"}}};
        Settings.Channels["arke-worlds"].BaselineSha256=PlayerCatalogueProjection.Hash(Settings.Channels["arke-worlds"].BaselineCatalogueJson);
        var parties=new Mock<ICurrentPartyResolver>();parties.Setup(x=>x.GetCurrentPartyIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(()=>Party);
        Memberships.Setup(x=>x.GetPartyIdsAsync(Tenant,It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync((Guid _,Guid id,CancellationToken _)=>id==Creator?new Guid[]{CreatorParty}:new Guid[]{EditorParty});
        Permissions.Setup(x=>x.HasPermissionAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>Allowed);
        Private.SetupGet(x=>x.LocationId).Returns("private-test");
        Private.Setup(x=>x.OpenPackageAsync(Tenant,It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<long>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _,Guid session,string hash,long bytes,CancellationToken _)=>new VerifiedInput(_inputs[hash],hash,bytes));
        var configuration=Options.Create(Settings);var gate=Mock.Of<IModuleGate>();
        Creators=new(Db,tenants,Actor,parties.Object,Permissions.Object,gate,configuration);
        Reviews=new(Db,new EditorialReviewAuthorization(tenants,Actor,parties.Object,Memberships.Object,Permissions.Object,gate,configuration),Private.Object,configuration,Clock);
        var authority=new PublicationAuthority(Db,tenants,Actor,parties.Object,Memberships.Object,Permissions.Object,gate,configuration);
        var partyReader=new Mock<IPartyReader>();partyReader.Setup(x=>x.ExistsAsync(Tenant,It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Service=new(Db,authority,Store,configuration,Clock,partyReader.Object);
        Worker=new(Db,authority,Private.Object,Store,configuration,tenants,Clock,NullLogger<PublicationDeliveryHandler>.Instance);
        AsEditor();
    }
    private readonly Dictionary<string,string> _inputs=[];
    public void AsCreator(){Actor.IsAuthenticated=true;Actor.UserId=Creator;Actor.TenantId=Tenant;Party=CreatorParty;}
    public void AsEditor(){Actor.IsAuthenticated=true;Actor.UserId=Editor;Actor.TenantId=Tenant;Party=EditorParty;}
    public async Task<SubmissionReceipt> ApproveAsync(string kind="world",string? title=null)
    {
        var path=Path.Combine(AppContext.BaseDirectory,"PublishingFixtures",kind+".zip");
        var package=JsonSerializer.Deserialize<PackageVerificationResult>(await File.ReadAllTextAsync(Path.ChangeExtension(path,".json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        _inputs[package.ArchiveSha256]=path;
        AsCreator();var receipt=(await Creators.CreateAsync(new(Guid.NewGuid(),"arke-worlds",title??package.WorldId,"Synthetic creator","A quiet adventure","en",package.WorldId,package.Edition,kind+".zip",
            package.ArchiveBytes,package.ArchiveSha256,"rights-v1",true,new("children","Ages 7+",package.Kind,package.CoverAssetId!,"Synthetic fixture attribution",
                [new("Synthetic fixture use","test-evidence",package.Assets.Select(a=>a.Id).ToArray())])))).Submission;
        var sub=await Db.Submissions.SingleAsync(s=>s.TenantId==Tenant&&s.Id==receipt.Id);var revision=await Db.Revisions.SingleAsync(r=>r.Id==sub.CurrentRevisionId);
        var session=new PublicationUploadSession{TenantId=Tenant,CreatorPartyId=CreatorParty,SubmissionId=sub.Id,RevisionId=revision.Id,StorageLocationId="private-test",Status="uploaded",
            DeclaredBytes=package.ArchiveBytes,DeclaredSha256=package.ArchiveSha256,ReceivedBytes=package.ArchiveBytes,ExpiresAt=Clock.UtcNow.AddDays(1),PartSizeBytes=1048576,TotalParts=1};
        var report=new PublicationValidationReport{TenantId=Tenant,SubmissionId=sub.Id,RevisionId=revision.Id,OperationId=Guid.NewGuid(),Status="passed",Code="verified",CompletedAt=Clock.UtcNow,
            PackageSha256=package.ArchiveSha256,PackageBytes=package.ArchiveBytes,PolicyJson=JsonSerializer.Serialize(Settings.Verifier),ReaderCommit=package.ReaderCommit,VerifierImage=Settings.Verifier.Image,ResultJson=JsonSerializer.Serialize(package)};
        Db.UploadSessions.Add(session);Db.ValidationReports.Add(report);revision.CurrentUploadSessionId=session.Id;revision.CurrentValidationReportId=report.Id;revision.UploadedAt=Clock.UtcNow;revision.PackageVerifiedAt=Clock.UtcNow;sub.Status="in-review";
        if(!await Db.Namespaces.AnyAsync(n=>n.TenantId==Tenant&&n.WorldId==package.WorldId))Db.Namespaces.Add(new(){TenantId=Tenant,ChannelKey="arke-worlds",WorldId=package.WorldId,CreatorPartyId=CreatorParty});
        await Db.SaveChangesAsync();AsEditor();var detail=await Reviews.GetAsync(sub.Id);
        await Reviews.DecideAsync(sub.Id,new(Guid.NewGuid(),revision.Id,detail.Snapshot!.Digest,detail.Submission.Version,"approve","Reviewed synthetic package","Approved",new(true,true,true)));
        return receipt;
    }
    public Task<PublicationOperationResponse> InitialiseAsync()=>Service.InitialiseAsync("arke-worlds",new(Guid.NewGuid(),Settings.Channels["arke-worlds"].BaselineSha256,"Qualified synthetic baseline"));
    public async Task<PublishRevisionRequest> RequestAsync(Guid id){var p=await Service.PreviewAsync(id);return new(Guid.NewGuid(),p.RevisionId,p.DecisionId!.Value,p.ReviewDigest,p.Version,p.Serial!.Value,p.CurrentBaseFingerprint,p.CurrentSlotFingerprint,true,"Release reviewed synthetic edition");}
    public Task DeliverAsync(Guid id)=>Worker.HandleAsync(new(id,Clock.UtcNow,Tenant,id));
    public async Task<string> HeadAsync(){Db.ChangeTracker.Clear();var state=await Db.PublicationChannels.SingleAsync(c=>c.TenantId==Tenant);return(await Db.CatalogueSnapshots.SingleAsync(s=>s.TenantId==Tenant&&s.Id==state.HeadSnapshotId)).Json;}
    public async Task ExportAsync(string name)
    {
        var root=Environment.GetEnvironmentVariable("AONIK_PLAYER_QUALIFICATION_DIRECTORY");if(string.IsNullOrWhiteSpace(root))return;
        var output=Path.Combine(root,name);Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"catalogue.json"),await HeadAsync());
        foreach(var pair in Store.Bytes)await File.WriteAllBytesAsync(Path.Combine(output,pair.Key.ToString("D")),pair.Value);
    }
    public void Dispose()=>Db.Dispose();
    internal sealed class MutableClock:IClock{public DateTime UtcNow{get;set;}=new(2026,11,4,12,0,0,DateTimeKind.Utc);}
    internal sealed class MemoryPublicStore:IPublicationArtifactStore
    {
        public string LocationId=>"public-test";
        public readonly System.Collections.Concurrent.ConcurrentDictionary<Guid,byte[]> Bytes=new();
        public int Writes,Verifications;
        public Func<PublicationArtifact,Task>? BeforeVerify;
        public bool FailReadback{get;set;}
        public bool FailAfterWrite{get;set;}
        public async Task WriteAsync(PublicationArtifact artifact,Stream source,CancellationToken cancellationToken=default)
        {using var output=new MemoryStream();await source.CopyToAsync(output,cancellationToken);var data=output.ToArray();if(data.Length!=artifact.Bytes||Convert.ToHexStringLower(SHA256.HashData(data))!=artifact.Sha256)throw new IOException("Digest mismatch");
            if(Bytes.TryGetValue(artifact.Id,out var old)&&!old.SequenceEqual(data))throw new IOException("Overwrite");Bytes[artifact.Id]=data;Interlocked.Increment(ref Writes);if(FailAfterWrite)throw new IOException("Response lost after copy");}
        public Task<Stream> OpenAsync(PublicationArtifact artifact,CancellationToken cancellationToken=default)=>Task.FromResult<Stream>(new MemoryStream(Bytes[artifact.Id],false));
        public async Task VerifyPublicAsync(PublicationArtifact artifact,CancellationToken cancellationToken=default){Interlocked.Increment(ref Verifications);if(BeforeVerify is not null)await BeforeVerify(artifact);if(FailReadback)throw new IOException("Public fetch failed");if(!Bytes.ContainsKey(artifact.Id))throw new IOException("Missing bytes");}
    }
}
