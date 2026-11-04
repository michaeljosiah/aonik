using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.Publishing.Persistence;

namespace Aonik.Api.Tests;

public sealed class PublicationIntakeTestFactory : CustomWebApplicationFactory
{
    public Guid PublisherTenantId { get; } = Guid.NewGuid();
    public string PrivateRoot { get; } = Path.Combine(Path.GetTempPath(), "aonik-intake-http-" + Guid.NewGuid().ToString("N"));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.Configure<PublishingOptions>(options =>
        {
            options.Channels["arke-worlds"] = new PublishingChannelOptions { TenantId = PublisherTenantId, Enabled = true,
                NamespaceAdmissionEnabled = true, MaxPackageBytes = 1000, RightsDeclarationVersion = "test-v1" };
            options.Intake.PrivateRoot = PrivateRoot;
            options.Intake.MaxCreatorRetainedBytes = 1024;
            options.Intake.MaxTenantRetainedBytes = 10240;
            options.Intake.SingleShotThresholdBytes = 8;
            options.Intake.PartSizeBytes = 4;
        }));
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        var full = Path.GetFullPath(PrivateRoot);
        if (full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Directory.Exists(full)) Directory.Delete(full,true);
    }
}

public class PublicationIntakeEndpointTests(PublicationIntakeTestFactory factory) : IClassFixture<PublicationIntakeTestFactory>
{
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private async Task<(HttpClient Client, SubmissionReceipt Receipt, UploadReceipt Upload)> Begin(byte[] bytes)
    {
        var creator = await WorkspaceTestSeeding.SignInAsync(factory,factory.PublisherTenantId,"Creator",withPlan:false);
        var request = new CreateSubmissionRequest(Guid.NewGuid(),"arke-worlds","Lantern Garden","Creator","Edition","en",
            "world-"+Guid.NewGuid().ToString("N"),"2026-11-03","edition.zip",bytes.Length,Hash(bytes),"test-v1",true);
        var created = await creator.Client.PostAsJsonAsync("/publishing/submissions",request);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var receipt = (await created.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Submission;
        var upload = await creator.Client.PostAsync($"/publishing/submissions/{receipt.Id}/uploads",null);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        return (creator.Client,receipt,(await upload.Content.ReadFromJsonAsync<UploadReceipt>())!);
    }
    private static async Task<HttpResponseMessage> Part(HttpClient client, Guid submission, Guid session, byte[] bytes, string? hash = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,$"/publishing/submissions/{submission}/uploads/{session}/parts/1");
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new("application/octet-stream");
        request.Headers.Add("X-Part-Sha256",hash ?? Hash(bytes));
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task UploadAndDownload_Should_KeepPrivateBytesUnverifiedUntilIndependentValidation()
    {
        var bytes = new byte[] {1,2,3,4}; var (client,receipt,upload) = await Begin(bytes);
        (await Part(client,receipt.Id,upload.Id,bytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        var complete = await client.PostAsync($"/publishing/submissions/{receipt.Id}/uploads/{upload.Id}/complete",null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        var downloaded = await client.GetAsync($"/publishing/submissions/{receipt.Id}/package");
        downloaded.StatusCode.Should().Be(HttpStatusCode.OK);
        downloaded.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        downloaded.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        downloaded.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await downloaded.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
        var status = (await client.GetFromJsonAsync<SubmissionReceipt>($"/publishing/submissions/{receipt.Id}"))!;
        status.Revision.PackageVerified.Should().BeFalse(); status.Status.Should().Be("uploaded");
    }

    [Fact]
    public async Task ChangedPartRetryAndIncompleteCompletion_Should_BeRefused()
    {
        var (client,receipt,upload) = await Begin([1,2,3,4]);
        (await client.PostAsync($"/publishing/submissions/{receipt.Id}/uploads/{upload.Id}/complete",null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Part(client,receipt.Id,upload.Id,[1,2,3,4])).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Part(client,receipt.Id,upload.Id,[9,9,9,9])).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Part(client,receipt.Id,upload.Id,[9,9,9,9],Hash([1,2,3,4]))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PrivatePackageAndPreview_Should_RejectOtherCreatorsAndAnonymousReaders()
    {
        var (owner,receipt,upload) = await Begin([1,2,3,4]);
        (await Part(owner,receipt.Id,upload.Id,[1,2,3,4])).StatusCode.Should().Be(HttpStatusCode.OK);
        await owner.PostAsync($"/publishing/submissions/{receipt.Id}/uploads/{upload.Id}/complete",null);
        foreach (var tenant in new[] {factory.PublisherTenantId,Guid.NewGuid()})
        {
            var other = await WorkspaceTestSeeding.SignInAsync(factory,tenant,"Other",withPlan:false);
            foreach (var suffix in new[] {"package","preview",$"uploads/{upload.Id}"})
                (await other.Client.GetAsync($"/publishing/submissions/{receipt.Id}/{suffix}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await Part(other.Client,receipt.Id,upload.Id,[1,2,3,4])).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        var anonymous = factory.CreateClient();
        (await anonymous.GetAsync($"/publishing/submissions/{receipt.Id}/package")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"/storage/publications/{receipt.Id}/package.zip")).StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Submit_Should_CommitOutboxAndHoldWhenVerifierIsUnconfigured()
    {
        var (client,receipt,upload) = await Begin([1,2,3,4]);
        await Part(client,receipt.Id,upload.Id,[1,2,3,4]);
        await client.PostAsync($"/publishing/submissions/{receipt.Id}/uploads/{upload.Id}/complete",null);
        var operation = Guid.NewGuid();
        var submitted = await client.PostAsJsonAsync($"/publishing/submissions/{receipt.Id}/submit",new {revisionId=receipt.Revision.Id,operationId=operation});
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
        var pending = (await submitted.Content.ReadFromJsonAsync<ValidationPreview>())!;
        pending.Status.Should().Be("pending");
        using (var scope = factory.Services.CreateScope())
        {
            var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>(); tenant.TenantId=factory.PublisherTenantId;
            var db = (PublishingDbContext)scope.ServiceProvider.GetRequiredService<IPublishingDataContext>();
            var message = db.Set<OutboxMessage>().Single(m=>m.EventId==pending.ReportId);
            message.TenantId.Should().Be(factory.PublisherTenantId);
            await scope.ServiceProvider.GetRequiredService<IEventHandler<PublicationValidationRequestedEvent>>().HandleAsync(
                new(pending.ReportId,DateTime.UtcNow,factory.PublisherTenantId,receipt.Id,receipt.Revision.Id,pending.ReportId));
        }
        var preview = (await client.GetFromJsonAsync<ValidationPreview>($"/publishing/submissions/{receipt.Id}/preview"))!;
        preview.Status.Should().Be("held"); preview.Code.Should().Be("verifier-unavailable");
        var current = (await client.GetFromJsonAsync<SubmissionReceipt>($"/publishing/submissions/{receipt.Id}"))!;
        current.Status.Should().Be("held"); current.Revision.PackageVerified.Should().BeFalse();
        (await client.DeleteAsync($"/publishing/submissions/{receipt.Id}/uploads/{upload.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
