using System.Net.Http.Json;
using System.Net;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Api.Tests;

public sealed class PublishingTestFactory : CustomWebApplicationFactory
{
    public Guid PublisherTenantId { get; } = Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.Configure<PublishingOptions>(options =>
            options.Channels["arke-worlds"] = new PublishingChannelOptions
            {
                TenantId = PublisherTenantId, Enabled = true, MaxPackageBytes = 100_000_000,
                RightsDeclarationVersion = "test-v1",
            }));
    }
}

public class PublishingSubmissionEndpointsTests(PublishingTestFactory factory) : IClassFixture<PublishingTestFactory>
{
    private static CreateSubmissionRequest Request() => new(Guid.NewGuid(), "arke-worlds", "The Lantern Garden",
        "Example Creator", "An illustrated edition.", "en", "lantern-garden", "2026-11-03",
        "lantern-garden.zip", 1024, new string('a', 64), "test-v1", true);

    [Fact]
    public async Task SubmissionRoutes_Should_RequireAuthentication()
    {
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/publishing/submissions", Request())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/publishing/submissions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateGetListAndReplay_Should_ReturnUnverifiedPrivateReceipt()
    {
        var person = await WorkspaceTestSeeding.SignInAsync(factory, factory.PublisherTenantId, "Creator", withPlan: false);
        var request = Request();
        var response = await person.Client.PostAsJsonAsync("/publishing/submissions", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<CreateSubmissionResult>())!;
        created.Inserted.Should().BeTrue();
        created.Submission.Status.Should().Be("awaiting-upload");
        created.Submission.Revision.PackageVerified.Should().BeFalse();
        response.Headers.Location!.ToString().Should().EndWith($"/publishing/submissions/{created.Submission.Id}");

        var fetched = await person.Client.GetFromJsonAsync<SubmissionReceipt>($"/publishing/submissions/{created.Submission.Id}");
        fetched.Should().BeEquivalentTo(created.Submission);
        var list = await person.Client.GetFromJsonAsync<SubmissionPage>("/publishing/submissions");
        list!.Items.Should().ContainSingle(x => x.Id == created.Submission.Id);

        var replay = await person.Client.PostAsJsonAsync("/publishing/submissions", request);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replay.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Inserted.Should().BeFalse();
        var conflict = await person.Client.PostAsJsonAsync("/publishing/submissions", request with { Title = "Changed" });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OtherCreatorAndOtherTenant_Should_NotReadOrListReceipt()
    {
        var owner = await WorkspaceTestSeeding.SignInAsync(factory, factory.PublisherTenantId, "Owner", withPlan: false);
        var response = await owner.Client.PostAsJsonAsync("/publishing/submissions", Request());
        var receipt = (await response.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Submission;
        foreach (var tenant in new[] { factory.PublisherTenantId, Guid.NewGuid() })
        {
            var other = await WorkspaceTestSeeding.SignInAsync(factory, tenant, "Other", withPlan: false);
            (await other.Client.GetAsync($"/publishing/submissions/{receipt.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await other.Client.GetAsync($"/publishing/submissions/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await other.Client.GetFromJsonAsync<SubmissionPage>("/publishing/submissions"))!.Items.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Create_Should_DenyNoPartyAndReadOnlyWithoutSubmitPermission()
    {
        var noParty = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create()
            .WithTenant(factory.PublisherTenantId).WithRoles("PersonalUser").WithPermissions("Workspace.Use"));
        (await noParty.PostAsJsonAsync("/publishing/submissions", Request())).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var options = TestAuthOptions.Create().WithTenant(factory.PublisherTenantId).WithRoles("ReadOnly").WithPermissions("Workspace.Use");
        var readOnly = await factory.CreateAuthenticatedClientAsync(options);
        await WorkspaceTestSeeding.SeedPartyAsync(factory, factory.PublisherTenantId, options.UserId, "Observer");
        (await readOnly.PostAsJsonAsync("/publishing/submissions", Request())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Submission_Should_NotExposeValidationOrApprovalMutationRoutes()
    {
        var person = await WorkspaceTestSeeding.SignInAsync(factory, factory.PublisherTenantId, "Creator", withPlan: false);
        foreach (var suffix in new[] { "approve", "publish", "validation" })
            (await person.Client.PostAsJsonAsync($"/publishing/submissions/{Guid.NewGuid()}/{suffix}", new { passed = true }))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Should_RejectCallerAssignedStateOrOwnership()
    {
        var person = await WorkspaceTestSeeding.SignInAsync(factory, factory.PublisherTenantId, "Creator", withPlan: false);
        var request = System.Text.Json.JsonSerializer.SerializeToNode(Request(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        request["status"] = "approved";
        request["creatorPartyId"] = Guid.NewGuid();
        (await person.Client.PostAsJsonAsync("/publishing/submissions", request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await person.Client.GetFromJsonAsync<SubmissionPage>("/publishing/submissions"))!.Items.Should().BeEmpty();
    }
}
