using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions.Agents;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Spec 030 — dispatcher contract tests. The dispatcher itself does not throw
/// when a handler reports <c>Applied = false</c> (it returns the result so the
/// approval service can revert and surface HTTP 422); that revert + 422
/// behaviour lives in <see cref="ProposalApprovalServiceDispatcherTests"/>.
/// </summary>
public class ProposalDispatcherTests
{
    private static AgentProposalDetail SampleProposal(string proposalType = "TestType") =>
        new(
            Id: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            ProposalType: proposalType,
            Status: "Approved",
            PayloadJson: "{}",
            ImpactSummary: "test");

    private sealed class StubHandler : IProposalHandler
    {
        private readonly Func<AgentProposalDetail, Task<ProposalHandlerResult>> _impl;
        public StubHandler(string proposalType, Func<AgentProposalDetail, Task<ProposalHandlerResult>> impl)
        {
            ProposalType = proposalType;
            _impl = impl;
        }

        public string ProposalType { get; }

        public Task<ProposalHandlerResult> HandleAsync(AgentProposalDetail proposal, CancellationToken cancellationToken)
            => _impl(proposal);
    }

    private sealed class StubRejectionHandler : IProposalRejectionHandler
    {
        public Func<AgentProposalDetail, Task> Impl { get; init; } = _ => Task.CompletedTask;
        public int CallCount;
        public string ProposalType { get; init; } = "TestType";

        public Task HandleRejectionAsync(AgentProposalDetail proposal, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            return Impl(proposal);
        }
    }

    [Fact]
    public async Task DispatchAsync_Should_ReturnHandlerResult_When_HandlerRegistered()
    {
        var resourceId = Guid.NewGuid();
        var handler = new StubHandler(
            "TestType",
            _ => Task.FromResult(new ProposalHandlerResult(
                Applied: true,
                AppliedResourceType: "Widget",
                AppliedResourceId: resourceId,
                Message: "done")));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IProposalHandler>("TestType", handler);
        var dispatcher = new ProposalDispatcher(services.BuildServiceProvider());

        var result = await dispatcher.DispatchAsync(SampleProposal(), CancellationToken.None);

        result.Applied.Should().BeTrue();
        result.AppliedResourceType.Should().Be("Widget");
        result.AppliedResourceId.Should().Be(resourceId);
        result.Message.Should().Be("done");
    }

    [Fact]
    public async Task DispatchAsync_Should_Throw_NoProposalHandlerRegistered_When_NothingRegisteredForType()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new ProposalDispatcher(services);

        var proposal = SampleProposal("UnknownType");
        var act = () => dispatcher.DispatchAsync(proposal, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<NoProposalHandlerRegisteredException>()).Which;
        exception.ProposalType.Should().Be("UnknownType");
    }

    [Fact]
    public async Task DispatchAsync_Should_PropagateHandlerException_AsIs()
    {
        var handler = new StubHandler(
            "TestType",
            _ => throw new InvalidOperationException("boom"));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IProposalHandler>("TestType", handler);
        var dispatcher = new ProposalDispatcher(services.BuildServiceProvider());

        var act = () => dispatcher.DispatchAsync(SampleProposal(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task DispatchAsync_Should_ReturnAppliedFalseResult_Unchanged()
    {
        var handler = new StubHandler(
            "TestType",
            _ => Task.FromResult(new ProposalHandlerResult(
                Applied: false,
                Message: "payload references a deleted entity")));

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IProposalHandler>("TestType", handler);
        var dispatcher = new ProposalDispatcher(services.BuildServiceProvider());

        // Dispatcher returns Applied = false; converting to
        // ProposalExecutionFailedException is the approval service's job.
        var result = await dispatcher.DispatchAsync(SampleProposal(), CancellationToken.None);

        result.Applied.Should().BeFalse();
        result.Message.Should().Be("payload references a deleted entity");
    }

    [Fact]
    public async Task RejectionDispatcher_Should_InvokeHandler_When_Registered()
    {
        var handler = new StubRejectionHandler { ProposalType = "TestType" };

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IProposalRejectionHandler>("TestType", handler);
        var dispatcher = new ProposalRejectionDispatcher(services.BuildServiceProvider());

        await dispatcher.DispatchAsync(SampleProposal(), CancellationToken.None);

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task RejectionDispatcher_Should_NoOp_When_NoHandlerRegistered()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new ProposalRejectionDispatcher(services);

        // No registered handler is NOT an error; the dispatcher just returns.
        var act = () => dispatcher.DispatchAsync(SampleProposal("NoRejectionHandlerType"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RejectionDispatcher_Should_PropagateHandlerException()
    {
        var handler = new StubRejectionHandler
        {
            ProposalType = "TestType",
            Impl = _ => throw new InvalidOperationException("cleanup broken")
        };

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IProposalRejectionHandler>("TestType", handler);
        var dispatcher = new ProposalRejectionDispatcher(services.BuildServiceProvider());

        var act = () => dispatcher.DispatchAsync(SampleProposal(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("cleanup broken");
    }
}
