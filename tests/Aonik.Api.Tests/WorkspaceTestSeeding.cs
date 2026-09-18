using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.Subscriptions.Contracts.Models;
using Aonik.Subscriptions.Contracts.Services;
using Aonik.Workspaces.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// What a workspace endpoint test needs standing behind the API: a user with a party, a plan whose
/// entitlements cover the two workspace meters, and a subscription binding the party to it. Everything
/// goes through the real services in a scope that impersonates the user, so the seeding itself obeys
/// the same authorisation the endpoints will.
/// </summary>
internal static class WorkspaceTestSeeding
{
    /// <summary>A person the test signs in as: a user, a party and the link between them.</summary>
    public sealed record Person(Guid UserId, Guid PartyId, HttpClient Client);

    public static async Task<Person> SignInAsync(
        CustomWebApplicationFactory factory, Guid tenantId, string displayName, bool withPlan = true)
    {
        // A permission makes the factory seed the User row the party link points at.
        var options = TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser").WithPermissions("Workspace.Use");
        var client = await factory.CreateAuthenticatedClientAsync(options);
        var userId = options.UserId;

        var partyId = await SeedPartyAsync(factory, tenantId, userId, displayName);

        if (withPlan)
        {
            await SubscribeAsync(factory, tenantId, userId, new SubscriberRef(SubscriberKinds.Party, partyId));
        }

        return new Person(userId, partyId, client);
    }

    public static async Task<Guid> SeedPartyAsync(
        CustomWebApplicationFactory factory, Guid tenantId, Guid userId, string displayName)
    {
        await using var scope = Impersonate(factory, tenantId, userId);
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var partyId = Guid.NewGuid();

        platform.Parties.Add(new Party
        {
            Id = partyId,
            TenantId = tenantId,
            PartyType = "Individual",
            DisplayName = displayName,
            Status = "Active",
        });

        platform.UserParties.Add(new UserParty
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
            LinkType = "Individual",
        });

        await platform.SaveChangesAsync();
        return partyId;
    }

    /// <summary>
    /// A plan granting a handful of workspaces and a modest byte ceiling, published once per tenant, and a
    /// subscription for the subscriber. The user must be able to manage billing for that subscriber.
    /// </summary>
    public static async Task SubscribeAsync(
        CustomWebApplicationFactory factory,
        Guid tenantId,
        Guid userId,
        SubscriberRef subscriber,
        int workspaceAllowance = 5,
        long byteAllowance = 50_000_000)
    {
        await using var scope = Impersonate(factory, tenantId, userId);
        var catalogue = scope.ServiceProvider.GetRequiredService<ICatalogueService>();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();

        var planCode = $"ws-test-{workspaceAllowance}-{byteAllowance}";
        var plans = await catalogue.ListPlansAsync();

        if (plans.All(plan => plan.Code != planCode))
        {
            var meters = await catalogue.ListMetersAsync();
            if (meters.All(meter => meter.Code != WorkspaceMeters.Count))
            {
                await catalogue.CreateMeterAsync(new CreateMeterRequest(WorkspaceMeters.Count, "Workspaces", MeterKinds.Ceiling, "workspaces"));
            }
            if (meters.All(meter => meter.Code != WorkspaceMeters.Bytes))
            {
                await catalogue.CreateMeterAsync(new CreateMeterRequest(WorkspaceMeters.Bytes, "Workspace bytes", MeterKinds.Ceiling, "bytes"));
            }

            var plan = await catalogue.CreatePlanAsync(new CreatePlanRequest(planCode, "Workspace test plan", BillingIntervals.None));
            var draft = await catalogue.CreateDraftVersionAsync(plan.Id, new CreatePlanVersionRequest(0m, "USD"));
            await catalogue.SetEntitlementsAsync(draft.Id, new SetEntitlementsRequest(
            [
                new PlanEntitlementSpec(WorkspaceMeters.Count, workspaceAllowance, ResetPolicies.Never),
                new PlanEntitlementSpec(WorkspaceMeters.Bytes, byteAllowance, ResetPolicies.Never),
            ]));
            await catalogue.PublishVersionAsync(draft.Id);
        }

        await subscriptions.SubscribeAsync(subscriber, planCode);
    }

    /// <summary>
    /// A family group as a product would stand it up: the owner creates it, invites the other adult, who
    /// accepts as themselves. Invitations have no route in this slice, so the two halves go through the
    /// service in scopes impersonating each person.
    /// </summary>
    public static async Task<Guid> FamilyGroupAsync(
        CustomWebApplicationFactory factory, Guid tenantId, Person owner, string name, params Person[] managers)
    {
        Guid groupId;

        await using (var scope = Impersonate(factory, tenantId, owner.UserId))
        {
            var groups = scope.ServiceProvider.GetRequiredService<IGroupService>();
            groupId = (await groups.CreateAsync(new CreateGroupCommand(GroupKinds.Family, name))).Id;
        }

        foreach (var manager in managers)
        {
            Guid membershipId;

            await using (var scope = Impersonate(factory, tenantId, owner.UserId))
            {
                var groups = scope.ServiceProvider.GetRequiredService<IGroupService>();
                membershipId = (await groups.InviteAsync(new InviteGroupMemberCommand(groupId, GroupRoles.Manager, PartyId: manager.PartyId))).Id;
            }

            await using (var scope = Impersonate(factory, tenantId, manager.UserId))
            {
                var groups = scope.ServiceProvider.GetRequiredService<IGroupService>();
                await groups.AcceptInvitationAsync(membershipId);
            }
        }

        return groupId;
    }

    /// <summary>A Spec 086 grant on one workspace, created as its owner.</summary>
    public static async Task GrantAsync(
        CustomWebApplicationFactory factory,
        Guid tenantId,
        Person owner,
        Guid workspaceId,
        Guid memberPartyId,
        string accessLevel)
    {
        await using var scope = Impersonate(factory, tenantId, owner.UserId);
        var grants = scope.ServiceProvider.GetRequiredService<IShareGrantService>();

        await grants.CreateGrantAsync(new CreateShareGrantCommand(
            ShareScopes.Entities,
            WorkspaceShareResource.Kind,
            [workspaceId],
            MemberPartyId: memberPartyId,
            AccessLevel: accessLevel));
    }

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>Upload one blob through the endpoint as the caller would: raw body, declared length.</summary>
    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid workspaceId, byte[] bytes, string? declaredHash = null)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new("application/octet-stream");
        content.Headers.ContentLength = bytes.Length;
        return await client.PutAsync($"/workspaces/{workspaceId}/blobs/{declaredHash ?? Sha256Hex(bytes)}", content);
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(HttpClient client, string path, T body)
        => client.PostAsJsonAsync(path, body);

    private static AsyncServiceScope Impersonate(CustomWebApplicationFactory factory, Guid tenantId, Guid userId)
    {
        var scope = factory.Services.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenant.TenantId = tenantId;
        tenant.ResolutionSource = "test";

        var user = scope.ServiceProvider.GetRequiredService<ICurrentUserContext>();
        user.UserId = userId;
        user.TenantId = tenantId;
        user.IsAuthenticated = true;
        user.Roles = ["PersonalUser"];

        return scope;
    }
}
