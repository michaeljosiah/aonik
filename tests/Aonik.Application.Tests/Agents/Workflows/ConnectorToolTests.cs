using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Agents.Framework;
using Aonik.Agents.Services.Workflows;
using Aonik.Application.Tests.Platform.Integrations;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Agents.Approval;

namespace Aonik.Application.Tests.Agents.Workflows;

public class ConnectorToolTests
{
    [Fact]
    public async Task AonikTools_Should_UseTheReadGateway_AndRequireServerPreparedConfirmationForRequests()
    {
        bool approved = false;
        ToolGateContext? prepared = null;
        var approvals = new Mock<IToolApprovalService>();
        approvals.Setup(service => service.GateAsync(It.IsAny<ToolGateContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolGateContext context, CancellationToken _) =>
            { prepared = context; return new(approved ? ToolGateDecision.ApprovedInline : ToolGateDecision.PendingApproval, Guid.NewGuid()); });
        using var scenario = new ConnectorScenario(configure: services =>
        {
            services.AddScoped<IConnectorService>(provider => provider.GetRequiredService<ConnectorService>());
            services.AddSingleton(approvals.Object);
            services.AddSingleton<IToolApprovalGate, ToolApprovalGate>();
            services.AddSingleton<IToolApprovalManifest, ConnectorToolApprovalManifest>();
            services.AddSingleton(Mock.Of<IToolApprovalAuditSink>());
            services.AddScoped<IAgentMutationGuard, ConnectorMutationGuard>();
        });
        await scenario.InitializeAsync();
        using var scope = scenario.Scope();
        AIFunction[] tools = ConnectorTools.CreateAll(scope.ServiceProvider).OfType<AIFunction>().ToArray();
        tools.Should().HaveCount(10);
        AIFunction create = tools.Single(tool => tool.Name == "aonik_create_connection_request");
        object? pending = await create.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["request"] = scenario.Request() }));

        pending.Should().NotBeNull();
        prepared!.Preview!.After!.Value.GetProperty("requiredScopes").EnumerateArray().Select(value => value.GetString()).Should().Contain(GmailActionKeys.ReadScope);
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await db.ConnectorConnectionRequests.CountAsync()).Should().Be(0);
        scenario.OAuth.Verify(provider => provider.CreateAuthorization(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()), Times.Never);
        approved = true;

        await create.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["request"] = scenario.Request() }));

        (await db.ConnectorConnectionRequests.CountAsync()).Should().Be(1);
        (await db.ConnectorConnectionGrants.CountAsync()).Should().Be(0);
        AIFunction read = tools.Single(tool => tool.Name == "gmail_search_messages");
        Func<Task> unauthorized = async () => await read.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            { ["connectionId"] = Guid.NewGuid(), ["request"] = new GmailSearchRequest("invoice") }));
        await unauthorized.Should().ThrowAsync<ConnectorAccessException>();
        scenario.Actions.Verify(provider => provider.InvokeAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
