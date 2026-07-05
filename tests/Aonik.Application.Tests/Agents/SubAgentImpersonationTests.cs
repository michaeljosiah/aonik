using Aonik.Finance.Agents;
using Aonik.PersonalFinance.Agents.CodeAct;
using Aonik.Finance.Agents.Tools;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Finance;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Regression tests for the Spec 025 sub-agent impersonation propagation fix
/// (re-implementation of the closed-without-merging PR #91, branch
/// <c>claude/charming-golick-01ee3a</c>, commit <c>35683459</c>).
/// </summary>
/// <remarks>
/// <para>
/// The bug: in the AI Playground, an admin impersonating a real user via the
/// User Brief picker sets <see cref="ICurrentUserContext.UserId"/> on the
/// request scope. Simi answering directly with parent tools correctly saw the
/// impersonated user's data. Routing the same question through
/// <c>pf_run_insights</c>/<c>pf_run_forecast</c>/<c>pf_run_classify_review</c>
/// to the corresponding Spec 025 sub-agent risked silently falling back to
/// whatever the scoped <see cref="ICurrentUserContext"/>/<see cref="ITenantContext"/>
/// resolved to at the moment the sub-agent actually ran its tools, rather than
/// the value the parent captured when it decided to delegate.
/// </para>
/// <para>
/// Two invocation paths carry the hazard:
/// <list type="bullet">
///   <item><b>ACA Sessions path</b> — <see cref="CodeActSandboxContextFactory"/>
///   bakes <see cref="ICurrentUserContext.UserId"/>/<see cref="ITenantContext.TenantId"/>
///   into the nonce at sub-agent build time. See
///   <see cref="Resolve_ShouldPreferSnapshot_OverAmbientScope"/> and its
///   Theory variants (one per Spec 025 sub-agent name).</item>
///   <item><b>Tool-loop fallback path</b> (no CodeAct provider configured) —
///   each host <see cref="AIFunction"/> is invoked directly by the LLM. See
///   <see cref="BuildWithImpersonation_ShouldWrapToolsWithContextRestoration_ForEachSubAgent"/>,
///   which simulates the exact drift the bug describes: the ambient scope is
///   mutated back to the admin's identity <i>after</i> the sub-agent is built
///   but <i>before</i> its tool is invoked, and asserts the invoked tool still
///   observes the parent's snapshot.</item>
/// </list>
/// </para>
/// </remarks>
public class SubAgentImpersonationTests
{
    private static readonly Guid AmbientAdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AmbientAdminTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ImpersonatedUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ImpersonatedTenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // ── ACA Sessions path: CodeActSandboxContextFactory ────────────────────

    [Theory]
    [InlineData("pf-insights")]
    [InlineData("pf-forecast")]
    [InlineData("pf-classify")]
    public void Resolve_ShouldPreferSnapshot_OverAmbientScope(string subAgentName)
    {
        // Arrange: the ambient scope holds the admin's identity (as if some
        // other consumer of the same DI scope had reset it, or the sub-agent
        // build simply runs after the scope drifted) while the parent's
        // snapshot carries the impersonated identity it captured earlier.
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new TenantContextFake { TenantId = AmbientAdminTenantId });
        services.AddSingleton<ICurrentUserContext>(new CurrentUserContextFake { UserId = AmbientAdminUserId });
        var sp = services.BuildServiceProvider();

        var snapshot = new SubAgentImpersonationSnapshot(ImpersonatedUserId, ImpersonatedTenantId);

        // Act
        var context = CodeActSandboxContextFactory.Resolve(sp, subAgentName, snapshot);

        // Assert: the nonce-bound context reflects the parent's snapshot, not
        // the ambient (drifted) scope — this is what the ACA Sessions
        // callback later bakes onto ITenantContext/ICurrentUserContext, so
        // getting this value right is the entire fix for that path.
        context.CurrentUserId.Should().Be(ImpersonatedUserId);
        context.TenantId.Should().Be(ImpersonatedTenantId);
        context.SubAgentName.Should().Be(subAgentName);
    }

    [Fact]
    public void Resolve_ShouldFallBackToAmbientScope_WhenNoSnapshotOverridePresent()
    {
        // Arrange: ordinary (non-impersonated) case — the caller IS the end
        // user, so CaptureImpersonationSnapshot's UserId/TenantId simply match
        // whatever the ambient scope already holds. Empty.HasOverride is
        // false, so this must behave exactly like the pre-fix Resolve(sp, name)
        // overload: read straight from the scope.
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new TenantContextFake { TenantId = AmbientAdminTenantId });
        services.AddSingleton<ICurrentUserContext>(new CurrentUserContextFake { UserId = AmbientAdminUserId });
        var sp = services.BuildServiceProvider();

        // Act
        var withEmptySnapshot = CodeActSandboxContextFactory.Resolve(sp, "pf-insights", SubAgentImpersonationSnapshot.Empty);
        var withNullSnapshot = CodeActSandboxContextFactory.Resolve(sp, "pf-insights", snapshot: null);
        var withOriginalOverload = CodeActSandboxContextFactory.Resolve(sp, "pf-insights");

        // Assert: all three resolve identically to the ambient scope — the
        // fix must not change behaviour for the ordinary, non-impersonated
        // production path (Payabo end users are never "impersonated").
        withEmptySnapshot.CurrentUserId.Should().Be(AmbientAdminUserId);
        withEmptySnapshot.TenantId.Should().Be(AmbientAdminTenantId);
        withNullSnapshot.Should().BeEquivalentTo(withEmptySnapshot, opts => opts.Excluding(c => c.RunId));
        withOriginalOverload.Should().BeEquivalentTo(withEmptySnapshot, opts => opts.Excluding(c => c.RunId));
    }

    [Fact]
    public void Resolve_ShouldPreferPartialSnapshot_WhenOnlyUserIdOverridden()
    {
        // A snapshot can carry just one of the two fields (e.g. a tenant with
        // no per-user impersonation active but a stale ambient tenant read).
        // Each field must be preferred independently.
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new TenantContextFake { TenantId = AmbientAdminTenantId });
        services.AddSingleton<ICurrentUserContext>(new CurrentUserContextFake { UserId = AmbientAdminUserId });
        var sp = services.BuildServiceProvider();

        var userOnlySnapshot = new SubAgentImpersonationSnapshot(ImpersonatedUserId, TenantId: null);

        var context = CodeActSandboxContextFactory.Resolve(sp, "pf-forecast", userOnlySnapshot);

        context.CurrentUserId.Should().Be(ImpersonatedUserId, "the snapshot's UserId must win");
        context.TenantId.Should().Be(AmbientAdminTenantId, "no TenantId override was given, so the ambient scope's tenant is used");
    }

    // ── Tool-loop fallback path: ContextRestoringAIFunction ────────────────

    [Theory]
    [InlineData("pf-insights")]
    [InlineData("pf-forecast")]
    [InlineData("pf-classify")]
    public async Task BuildWithImpersonation_ShouldWrapToolsWithContextRestoration_ForEachSubAgent(string subAgentName)
    {
        // Arrange: build the real descriptor for each Spec 025 sub-agent with
        // a NullCodeActSandboxProvider registered, forcing the tool-loop
        // fallback path (no execute_code tool — host tools registered
        // directly, exactly as production does whenever ACA Sessions/
        // Hyperlight can't service the request).
        var (tenantContext, userContext, snapshotReader, sp) = CreateFixture();

        // The ambient scope starts holding the impersonated identity (as it
        // would immediately after PlaygroundStreamingEndpoint sets
        // _currentUserContext.UserId = request.ImpersonateUserId).
        userContext.UserId = ImpersonatedUserId;
        tenantContext.TenantId = ImpersonatedTenantId;

        var snapshot = new SubAgentImpersonationSnapshot(ImpersonatedUserId, ImpersonatedTenantId);
        var descriptor = ResolveRealDescriptor(sp, subAgentName);
        var chatClient = new ToolCapturingChatClient();

        var agent = descriptor.BuildWithImpersonation(
            chatClient: chatClient,
            serviceProvider: sp,
            instructionsOverride: null,
            allowedToolNames: null,
            snapshot: snapshot);

        var probeToolName = ProbeToolNameFor(subAgentName);
        var probeTool = await RunAndFindToolAsync(agent, chatClient, probeToolName);

        // Simulate the exact drift the bug describes: something sharing the
        // same scope resets the ambient identity back to the admin's BEFORE
        // the tool actually runs (a fresh continuation, another consumer of
        // the same scoped services, or simply time passing mid-run). The old,
        // unpatched Build() had no way to notice or correct this.
        userContext.UserId = AmbientAdminUserId;
        tenantContext.TenantId = AmbientAdminTenantId;

        // Act
        await probeTool.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        // Assert: ContextRestoringAIFunction repairs the scoped contexts
        // in place immediately before delegating to the wrapped tool — this
        // holds for every sub-agent regardless of which specific tool is
        // probed, because the restoration happens in the wrapper, not inside
        // the wrapped tool's own logic.
        userContext.UserId.Should().Be(
            ImpersonatedUserId,
            "the tool-loop fallback path must re-apply the parent's snapshot before every tool call, " +
            "even when the ambient scope drifted back to the admin's identity after the sub-agent was built");
        tenantContext.TenantId.Should().Be(ImpersonatedTenantId);

        if (string.Equals(probeToolName, "pf_list_snapshot_history", StringComparison.Ordinal))
        {
            // pf-insights / pf-forecast: pf_list_snapshot_history reads
            // _currentUserProvider.GetCurrentUserId() directly, so this
            // additionally proves the restored context actually reaches the
            // tool's own business logic — not just the scoped contexts in
            // isolation.
            snapshotReader.LastRequestedUserId.Should().Be(ImpersonatedUserId);
        }
    }

    [Theory]
    [InlineData("pf-insights")]
    [InlineData("pf-forecast")]
    [InlineData("pf-classify")]
    public async Task BuildWithImpersonation_ShouldNotTouchScope_WhenNoImpersonationActive(string subAgentName)
    {
        // Ordinary production path: the caller IS the end user, so the
        // snapshot has no override (both fields null — CaptureImpersonationSnapshot
        // returns this whenever ICurrentUserProvider/ITenantProvider simply
        // reflect the caller's own identity). ContextRestoringAIFunction must
        // no-op — proves the fix is inert for the overwhelming common case
        // (every real Payabo end-user turn, not just the playground).
        var (tenantContext, userContext, snapshotReader, sp) = CreateFixture();

        userContext.UserId = ImpersonatedUserId; // "impersonated" here just means "the one real user"
        tenantContext.TenantId = ImpersonatedTenantId;

        var descriptor = ResolveRealDescriptor(sp, subAgentName);
        var chatClient = new ToolCapturingChatClient();
        var agent = descriptor.BuildWithImpersonation(
            chatClient, sp, instructionsOverride: null, allowedToolNames: null,
            snapshot: SubAgentImpersonationSnapshot.Empty);

        var probeToolName = ProbeToolNameFor(subAgentName);
        var tool = await RunAndFindToolAsync(agent, chatClient, probeToolName);

        await tool.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        userContext.UserId.Should().Be(ImpersonatedUserId);
        tenantContext.TenantId.Should().Be(ImpersonatedTenantId);

        if (string.Equals(probeToolName, "pf_list_snapshot_history", StringComparison.Ordinal))
        {
            snapshotReader.LastRequestedUserId.Should().Be(ImpersonatedUserId);
            snapshotReader.InvocationCount.Should().Be(1);
        }
    }

    /// <summary>
    /// Picks a whitelisted, all-optional-parameters read tool for each Spec
    /// 025 sub-agent to invoke directly in these tests. No single tool name
    /// is shared across all three whitelists (see <c>InsightsSubAgentToolNames</c>
    /// / <c>ForecastSubAgentToolNames</c> / <c>ClassifySubAgentToolNames</c> in
    /// <c>PersonalFinanceTools.cs</c>), so the probe differs per sub-agent —
    /// which tool is probed is immaterial to what's being proven here, since
    /// <c>ContextRestoringAIFunction</c> restores the scoped contexts before
    /// delegating to ANY wrapped tool, not specifically this one.
    /// </summary>
    private static string ProbeToolNameFor(string subAgentName) => subAgentName switch
    {
        "pf-classify" => "pf_list_classification_review_queue",
        _ => "pf_list_snapshot_history", // pf-insights and pf-forecast both whitelist this one.
    };

    [Fact]
    public void BuildWithImpersonation_ShouldBeUnavailable_ForCompassPlanner()
    {
        // pf-compass-planner (Spec 021) never resolves the scoped user/tenant
        // itself — it takes its financial context as a request payload — so
        // it deliberately does NOT implement ISubAgentDescriptor. Confirms the
        // fix's scope: exactly the three Spec 025 sub-agents, not every
        // IDomainAgentDescriptor.
        var descriptor = new CompassPlannerAgentDescriptor();
        descriptor.Should().NotBeAssignableTo<ISubAgentDescriptor>();
    }

    // ── Fixture plumbing ────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal-but-real DI container satisfying
    /// <see cref="PersonalFinanceTools.CreateAll"/>'s full dependency list plus
    /// the three <c>Pf*AgentDescriptor</c> registrations and a
    /// <see cref="NullCodeActSandboxProvider"/> (forces the tool-loop fallback
    /// path deterministically, matching how production behaves whenever no
    /// CodeAct provider is configured/available — the common case on Windows
    /// dev boxes and CI, per commit 69620409).
    /// </summary>
    private static (TenantContextFake TenantContext, CurrentUserContextFake UserContext, FakeSnapshotReader SnapshotReader, IServiceProvider ServiceProvider) CreateFixture()
    {
        var tenantContext = new TenantContextFake();
        var userContext = new CurrentUserContextFake();
        var snapshotReader = new FakeSnapshotReader();

        var services = new ServiceCollection();

        // Scoped identity contexts — settable, mutated directly by the test
        // to simulate drift and by ContextRestoringAIFunction to repair it.
        services.AddSingleton<ITenantContext>(tenantContext);
        services.AddSingleton<ICurrentUserContext>(userContext);

        // The read-only snapshot providers PersonalFinanceTools itself uses
        // to CAPTURE the snapshot — these delegate to the same scoped
        // contexts above, exactly like the real HttpContextTenantProvider /
        // HttpContextCurrentUserProvider do in production.
        services.AddSingleton<ITenantProvider>(new DelegatingTenantProvider(tenantContext));
        services.AddSingleton<ICurrentUserProvider>(new DelegatingCurrentUserProvider(userContext));

        // CodeAct: Null provider forces every descriptor onto the tool-loop
        // fallback path so we exercise ContextRestoringAIFunction, not the
        // ACA Sessions nonce path (covered separately above).
        services.AddSingleton<ICodeActSandboxProvider, NullCodeActSandboxProvider>();

        // IAgentConfigurationService: Moq stub returning null (no tenant
        // override configured) is the simplest, most common path —
        // BuildStructuredSubAgentAsync's config-driven instructionsOverride/
        // allowedToolNames branches are an orthogonal concern to this fix.
        services.AddSingleton(new Mock<IAgentConfigurationService>().Object);

        // The three Spec 025 sub-agent descriptors under test, plus
        // pf-compass-planner (registered so ResolveSubAgentDescriptor-style
        // lookups reflect the real DI shape — not required by these tests,
        // but keeps the fixture representative of FinanceModule's actual
        // registration list).
        services.AddSingleton<IDomainAgentDescriptor, PfInsightsAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, PfForecastAgentDescriptor>();
        services.AddSingleton<IDomainAgentDescriptor, PfClassifyAgentDescriptor>();

        // PersonalFinanceTools.CreateAll's remaining dependencies — none of
        // their business logic is ever invoked by these tests (only
        // pf_list_snapshot_history, backed by ICustomerInsightSnapshotReader,
        // actually executes), so plain Moq stubs are sufficient.
        services.AddSingleton(new Mock<IPersonalAccountService>().Object);
        services.AddSingleton(new Mock<IPersonalTransactionService>().Object);
        services.AddSingleton(new Mock<IBillService>().Object);
        services.AddSingleton(new Mock<IBudgetService>().Object);
        services.AddSingleton(new Mock<ICommitmentService>().Object);
        services.AddSingleton(new Mock<IPersonalFinanceInsightsService>().Object);
        services.AddSingleton(new Mock<IDashboardService>().Object);
        services.AddSingleton(new Mock<IFxRateHistoryReader>().Object);
        services.AddSingleton(new Mock<ITransactionClassificationService>().Object);
        services.AddSingleton(new Mock<IStatementImportService>().Object);
        services.AddSingleton(new Mock<ITransactionAttachmentService>().Object);
        services.AddSingleton<ICustomerInsightSnapshotReader>(snapshotReader);
        // Orders read/cancel through the customer-facing contract and resolve the
        // caller's party via IUserPartyResolver — the PF tools no longer touch
        // Finance's IOrderService or FinanceDbContext (Spec 027 S-Contracts / #118).
        services.AddSingleton(new Mock<ICustomerOrderService>().Object);
        services.AddSingleton(new Mock<IUserPartyResolver>().Object);
        services.AddSingleton(new Mock<IGoalService>().Object);
        services.AddSingleton(new Mock<ICompassPlanService>().Object);
        services.AddSingleton(new Mock<ICompassGuidanceService>().Object);
        // Unrelated to the per-test ToolCapturingChatClient passed directly to
        // descriptor.BuildWithImpersonation(...) below — this only satisfies
        // PersonalFinanceTools.CreateAll's own IChatClient dependency, which
        // these tests never exercise (they call BuildWithImpersonation
        // directly rather than going through PersonalFinanceTools.RunInsights
        // et al.'s BuildStructuredSubAgentAsync).
        services.AddSingleton<IChatClient>(new ToolCapturingChatClient());

        var sp = services.BuildServiceProvider();
        return (tenantContext, userContext, snapshotReader, sp);
    }

    /// <summary>
    /// Resolves the real <see cref="ISubAgentDescriptor"/> for the given
    /// Spec 025 sub-agent name from the fixture's DI container — the actual
    /// production type, not a stand-in, so these tests exercise the fix
    /// exactly as shipped.
    /// </summary>
    private static ISubAgentDescriptor ResolveRealDescriptor(IServiceProvider sp, string subAgentName)
    {
        var descriptor = sp.GetServices<IDomainAgentDescriptor>()
            .FirstOrDefault(d => string.Equals(d.Name, subAgentName, StringComparison.Ordinal));

        descriptor.Should().NotBeNull($"'{subAgentName}' must be registered in the fixture");
        descriptor.Should().BeAssignableTo<ISubAgentDescriptor>(
            $"'{subAgentName}' is one of the three Spec 025 sub-agents and must implement ISubAgentDescriptor");

        return (ISubAgentDescriptor)descriptor!;
    }

    /// <summary>
    /// Drives one turn through the built agent so the wired tool list becomes
    /// observable via <paramref name="chatClient"/>'s capture, then returns
    /// the named <see cref="AIFunction"/> from it.
    /// </summary>
    /// <remarks>
    /// <c>ChatClientAgent.ChatOptions</c> is declared <c>internal</c> in
    /// Microsoft.Agents.AI 1.9.0 (confirmed via reflection against the actual
    /// referenced package — not visible from this assembly despite appearing
    /// in the public-looking XML doc member list), so the wired tools cannot
    /// be read directly off the built agent from a test project. Capturing
    /// them as they are handed to <see cref="IChatClient.GetResponseAsync"/>
    /// uses the same public seam the framework itself relies on to reach the
    /// LLM, and is exactly how <c>agent.RunStreamingAsync(...)</c> surfaces
    /// tools to a real model in production — so this is a faithful path to
    /// the wired <see cref="AIFunction"/>, not a workaround around internals.
    /// </remarks>
    private static async Task<AIFunction> RunAndFindToolAsync(
        Microsoft.Agents.AI.AIAgent agent, ToolCapturingChatClient chatClient, string toolName)
    {
        await agent.RunAsync("probe", session: null, options: null, CancellationToken.None);

        chatClient.LastTools.Should().NotBeNull(
            "ChatClientAgent must hand its wired tools to IChatClient.GetResponseAsync on every run");

        var match = chatClient.LastTools!
            .OfType<AIFunction>()
            .FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.Ordinal));

        match.Should().NotBeNull($"'{toolName}' must be present among the wired host tools");
        return match!;
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal settable <see cref="ITenantContext"/> fake — mirrors the real
    /// <c>Aonik.Infrastructure.Multitenancy.TenantContext</c> shape (a plain
    /// mutable POCO) without requiring an <c>IHttpContextAccessor</c>.
    /// </summary>
    private sealed class TenantContextFake : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    /// <summary>
    /// Minimal settable <see cref="ICurrentUserContext"/> fake — mirrors the
    /// shape of the real <c>HttpContextCurrentUserContext</c> (a mutable,
    /// scoped POCO) without requiring an <c>IHttpContextAccessor</c>. Matches
    /// the inline <c>TestCurrentUserContext</c> convention already used in
    /// <c>IdentityServiceTests.cs</c>.
    /// </summary>
    private sealed class CurrentUserContextFake : ICurrentUserContext
    {
        public Guid? UserId { get; set; }
        public Guid? TenantId { get; set; }
        public string? ExternalIssuer { get; set; }
        public string? ExternalSubject { get; set; }
        public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
        public bool IsAuthenticated { get; set; }
    }

    /// <summary>
    /// Mirrors the real <c>HttpContextTenantProvider</c>: a read-only
    /// snapshot provider that delegates straight through to the mutable
    /// scoped <see cref="ITenantContext"/>, so mutating the context (as the
    /// test does to simulate drift, and as <c>ContextRestoringAIFunction</c>
    /// does to repair it) is immediately visible through this provider too.
    /// </summary>
    private sealed class DelegatingTenantProvider : ITenantProvider
    {
        private readonly ITenantContext _tenantContext;
        public DelegatingTenantProvider(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Guid GetCurrentTenantId() => _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant context not available");

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            if (_tenantContext.TenantId is { } id)
            {
                tenantId = id;
                return true;
            }
            tenantId = Guid.Empty;
            return false;
        }
    }

    /// <summary>Mirrors the real <c>HttpContextCurrentUserProvider</c> — see <see cref="DelegatingTenantProvider"/>.</summary>
    private sealed class DelegatingCurrentUserProvider : ICurrentUserProvider
    {
        private readonly ICurrentUserContext _userContext;
        public DelegatingCurrentUserProvider(ICurrentUserContext userContext) => _userContext = userContext;

        public Guid? GetCurrentUserId() => _userContext.UserId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userContext.UserId ?? Guid.Empty;
            return _userContext.UserId.HasValue;
        }
    }

    /// <summary>
    /// Stateful fake (not a strict mock) recording the UserId each call was
    /// made with — matches this codebase's existing convention (see
    /// <c>CompassTestSupport.cs</c>: "stateful fakes ... so tests assert
    /// observable behaviour") for asserting what a tool invocation actually
    /// saw, rather than verifying a mock setup.
    /// </summary>
    private sealed class FakeSnapshotReader : ICustomerInsightSnapshotReader
    {
        public Guid? LastRequestedUserId { get; private set; }
        public int InvocationCount { get; private set; }

        public Task<CustomerInsightSnapshotResponse?> GetCurrentSnapshotAsync(
            Guid userId, CancellationToken cancellationToken = default)
        {
            LastRequestedUserId = userId;
            InvocationCount++;
            return Task.FromResult<CustomerInsightSnapshotResponse?>(null);
        }

        public Task<CustomerInsightSnapshotResponse?> GetSnapshotAsync(
            Guid snapshotId, CancellationToken cancellationToken = default)
            => Task.FromResult<CustomerInsightSnapshotResponse?>(null);

        public Task<IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>> GetSnapshotHistoryAsync(
            Guid userId, int take = 20, CancellationToken cancellationToken = default)
        {
            LastRequestedUserId = userId;
            InvocationCount++;
            return Task.FromResult<IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>>(
                Array.Empty<CustomerInsightSnapshotHistoryItemResponse>());
        }
    }

    /// <summary>
    /// No-op <see cref="IChatClient"/> that also records the last
    /// <see cref="ChatOptions.Tools"/> it was handed. Never drives an actual
    /// chat completion loop — one <c>agent.RunAsync("probe", ...)</c> call is
    /// enough to capture the wired tool list (see <see cref="RunAndFindToolAsync"/>),
    /// after which the test invokes the specific returned <see cref="AIFunction"/>
    /// directly, mirroring how <c>AcaSessionsCodeActSandboxProviderTests</c>
    /// tests <c>TryBuildExecuteCodeTool</c>'s returned tool without ever
    /// touching a real LLM.
    /// </summary>
    private sealed class ToolCapturingChatClient : IChatClient
    {
        public IList<AITool>? LastTools { get; private set; }

        public ChatClientMetadata Metadata { get; } = new("ToolCapturingChatClient");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastTools = options?.Tools;
            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, string.Empty)]));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastTools = options?.Tools;
            await Task.CompletedTask;
            yield return new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent(string.Empty)] };
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceType == typeof(IChatClient) ? this : null;

        public void Dispose() { }
    }
}
