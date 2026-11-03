using System.Text.Json;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.Platform.Contracts.Models.Notifications;
using System.Net;
using System.Net.Http.Json;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Api.Tests;

public sealed class ChatRoomEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private async Task<(HttpClient Client, Guid UserId)> StaffAsync(Guid tenant, string role = "Operations")
    {
        var options = TestAuthOptions.Create().WithTenant(tenant).WithRoles(role).WithPermissions("Ai.Read");
        var client = await factory.CreateAuthenticatedClientAsync(options);
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant;
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var storedRole = await db.Roles.FirstOrDefaultAsync(candidate => candidate.TenantId == tenant && candidate.Name == role);
        if (storedRole is null) { storedRole = new Role { TenantId = tenant, Name = role }; db.Roles.Add(storedRole); }
        db.UserRoles.Add(new UserRole { UserId = options.UserId, RoleId = storedRole.Id });
        await db.SaveChangesAsync();
        return (client, options.UserId);
    }

    [Fact]
    public async Task TwoHumans_ShouldShareMessagesOnlyAfterExplicitInvitation_AndLoseAccessAfterRemoval()
    {
        var tenant = Guid.NewGuid();
        var (owner, _) = await StaffAsync(tenant);
        var (colleague, colleagueId) = await StaffAsync(tenant);
        var created = await owner.PostAsJsonAsync("/ai/rooms", new CreateChatRoomRequest("Collections review", [new("Invoice", "INV-2048")]));
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var room = (await created.Content.ReadFromJsonAsync<ChatRoomDetail>())!;
        (await colleague.GetAsync($"/ai/rooms/{room.Room.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/members", new AddChatRoomMemberRequest($"human:{colleagueId}", "Participant", true))).StatusCode.Should().Be(HttpStatusCode.OK);
        var request = new SendRoomMessageRequest(Guid.NewGuid(), "Maria: review the invoice reference", ["agent:master-orchestrator"]);
        var sent = await colleague.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/messages", request);
        sent.StatusCode.Should().Be(HttpStatusCode.OK);
        (await colleague.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/messages", request)).StatusCode.Should().Be(HttpStatusCode.OK);
        var shared = (await owner.GetFromJsonAsync<ChatRoomDetail>($"/ai/rooms/{room.Room.Id}"))!;
        shared.Messages.Count(message => message.Content == request.Content).Should().Be(1);
        shared.Messages.Single(message => message.Content == request.Content).AuthorUserId.Should().Be(colleagueId);
        (await owner.DeleteAsync($"/ai/rooms/{room.Room.Id}/members/{Uri.EscapeDataString($"human:{colleagueId}")}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await colleague.GetAsync($"/ai/rooms/{room.Room.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidRoomRequests_ShouldFailAtTheApiBoundary()
    {
        var tenant = Guid.NewGuid(); var (owner, _) = await StaffAsync(tenant);
        (await owner.PostAsJsonAsync("/ai/rooms", new { title = "Review", context = new object?[] { null } })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var room = (await (await owner.PostAsJsonAsync("/ai/rooms", new CreateChatRoomRequest("Review"))).Content.ReadFromJsonAsync<ChatRoomDetail>())!;
        (await owner.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/messages", new { clientRequestId = Guid.Empty, content = " " })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await owner.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/turns", new RunRoomAgentRequest(Guid.Empty, Guid.Empty, ""))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await owner.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/members", new AddChatRoomMemberRequest("", "Unknown", false))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await owner.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/approvals/decide", new DecideRoomApprovalRequest(Guid.Empty, Guid.Empty, "Maybe"))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ObserverAndReadOnly_ShouldNeverReachAgentExecution()
    {
        var tenant = Guid.NewGuid(); var (owner, _) = await StaffAsync(tenant);
        var (observer, observerId) = await StaffAsync(tenant, "ReadOnly");
        var room = (await (await owner.PostAsJsonAsync("/ai/rooms", new CreateChatRoomRequest("Review room"))).Content.ReadFromJsonAsync<ChatRoomDetail>())!;
        (await owner.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/members", new AddChatRoomMemberRequest($"human:{observerId}", "Observer", true))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await observer.GetAsync($"/ai/rooms/{room.Room.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await observer.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/messages", new SendRoomMessageRequest(Guid.NewGuid(), "Write"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await observer.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/turns", new RunRoomAgentRequest(Guid.NewGuid(), Guid.NewGuid(), "master-orchestrator"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CopiedGlobalApprovalLinks_ShouldNotBypassRoomRemoval()
    {
        var tenant = Guid.NewGuid(); var (owner, _) = await StaffAsync(tenant);
        var (colleague, colleagueId) = await StaffAsync(tenant);
        var room = (await (await owner.PostAsJsonAsync("/ai/rooms", new CreateChatRoomRequest("Approval room"))).Content.ReadFromJsonAsync<ChatRoomDetail>())!;
        (await owner.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/members", new AddChatRoomMemberRequest($"human:{colleagueId}", "Participant", true))).EnsureSuccessStatusCode();
        var prompt = (await (await colleague.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/messages", new SendRoomMessageRequest(Guid.NewGuid(), "Prepare an action"))).Content.ReadFromJsonAsync<ChatRoomMessageDto>())!;
        Guid proposalId, approvalId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant;
            var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            var aiRunId = Guid.NewGuid();
            var proposal = new Proposal { TenantId = tenant, AiRunId = aiRunId, ProposalType = "Finance.CapturePayment", RiskTier = "High", Status = ProposalStatus.Proposed };
            var approval = new ToolApprovalRequest { TenantId = tenant, AiRunId = aiRunId, ThreadId = room.Room.Id.ToString(), RequestingUserId = colleagueId, ProposalId = proposal.Id, RiskTier = "High", ToolName = "finance_capture_payment", ActionKind = "Capture payment", ExpiresAt = DateTime.UtcNow.AddMinutes(15) };
            db.Proposals.Add(proposal); db.ToolApprovalRequests.Add(approval);
            db.ChatRoomTurns.Add(new ChatRoomTurn { TenantId = tenant, RoomId = room.Room.Id, ClientRequestId = Guid.NewGuid(), PromptMessageId = prompt.Id, RequestedByUserId = colleagueId, AgentName = "master-orchestrator", AiRunId = aiRunId, Status = "Completed", StartedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(); proposalId = proposal.Id; approvalId = approval.Id;
        }
        (await owner.DeleteAsync($"/ai/rooms/{room.Room.Id}/members/{Uri.EscapeDataString($"human:{colleagueId}")}")).EnsureSuccessStatusCode();
        (await colleague.PostAsJsonAsync($"/ai/tool-approvals/{approvalId}/decide", new { Decision = "Approve" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await colleague.PostAsJsonAsync($"/ai/proposals/{proposalId}/approve", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await colleague.PostAsJsonAsync($"/ai/proposals/{proposalId}/dismiss", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OutboxNotifications_ShouldReachStaffInboxOnce_WithoutExposingMessageText()
    {
        var tenant = Guid.NewGuid(); var (owner, _) = await StaffAsync(tenant);
        var (observer, observerId) = await StaffAsync(tenant, "ReadOnly");
        var room = (await (await owner.PostAsJsonAsync("/ai/rooms", new CreateChatRoomRequest("Shared review"))).Content.ReadFromJsonAsync<ChatRoomDetail>())!;
        (await owner.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/members", new AddChatRoomMemberRequest($"human:{observerId}", "Observer", true))).EnsureSuccessStatusCode();
        var prompt = (await (await owner.PostAsJsonAsync($"/ai/rooms/{room.Room.Id}/messages", new SendRoomMessageRequest(Guid.NewGuid(), "Sensitive review text", [$"human:{observerId}"]))).Content.ReadFromJsonAsync<ChatRoomMessageDto>())!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant;
            var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            var envelope = await db.Set<OutboxMessage>().SingleAsync(value => value.EventId == prompt.Id);
            var posted = JsonSerializer.Deserialize<ChatRoomMessagePostedEvent>(envelope.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var handler = scope.ServiceProvider.GetServices<IEventHandler<ChatRoomMessagePostedEvent>>().Single();
            await handler.HandleAsync(posted); await handler.HandleAsync(posted);
        }
        var inbox = (await observer.GetFromJsonAsync<List<NotificationResponse>>("/admin/notifications"))!;
        inbox.Should().ContainSingle(); var notification = inbox.Single();
        notification.Type.Should().Be("chat.mention");
        notification.Body.Should().NotContain("Sensitive review text");
        notification.ActionUrl.Should().Contain($"message={prompt.Id:D}");
        (await owner.GetFromJsonAsync<List<NotificationResponse>>("/admin/notifications")).Should().BeEmpty();
        (await owner.PostAsync($"/admin/notifications/{notification.Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await observer.PostAsync($"/admin/notifications/{notification.Id}/read", null)).EnsureSuccessStatusCode();
        (await observer.GetFromJsonAsync<NotificationSummaryResponse>("/admin/notifications/summary"))!.UnreadCount.Should().Be(0);
        (await observer.PostAsync($"/admin/notifications/{notification.Id}/dismiss", null)).EnsureSuccessStatusCode();
        (await observer.GetFromJsonAsync<List<NotificationResponse>>("/admin/notifications")).Should().BeEmpty();
    }

    [Fact]
    public async Task PersonalUserAndCrossTenant_ShouldNotSeeStaffRoomsOrDirectory()
    {
        var tenant = Guid.NewGuid(); var (owner, _) = await StaffAsync(tenant);
        var room = (await (await owner.PostAsJsonAsync("/ai/rooms", new CreateChatRoomRequest("Private team room"))).Content.ReadFromJsonAsync<ChatRoomDetail>())!;
        var personal = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenant).WithRoles("PersonalUser"));
        (await personal.GetAsync("/ai/rooms/people")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await personal.GetAsync("/admin/notifications")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var (otherTenant, _) = await StaffAsync(Guid.NewGuid());
        (await otherTenant.GetAsync($"/ai/rooms/{room.Room.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await otherTenant.GetFromJsonAsync<List<ChatRoomSummary>>("/ai/rooms")).Should().BeEmpty();
    }
}
