#pragma warning disable MAAI001 // Qualify the experimental skill API used by the Agents module.

using System.Text;

using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;

namespace Aonik.Application.Tests.Agents.Tenant;

public class TenantSkillsProviderCompatibilityTests
{
    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("/absolute")]
    [InlineData("C:\\outside")]
    public async Task Materializer_Should_RejectPathLikeNames_BeforeReadingStorage(string name)
    {
        // Arrange
        var fileStore = new Mock<IFileStore>(MockBehavior.Strict);
        var materializer = new TenantSkillMaterializer(NullLogger<TenantSkillMaterializer>.Instance);

        // Act
        string? directory = await materializer.EnsureMaterializedAsync(Guid.NewGuid(), new TenantSkill
        {
            Id = Guid.NewGuid(),
            Name = name,
            StorageKey = "skill-key",
        }, fileStore.Object);

        // Assert
        directory.Should().BeNull();
        fileStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_Should_ExposeApprovedSkillReads_WithoutRequestingApprovalOrLoadingDraftSkills()
    {
        // Arrange
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Guid tenantId = Guid.NewGuid();
        var tenantProvider = new TestTenantProvider(tenantId);
        var dbOptions = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"MafSkills_{Guid.NewGuid():N}")
            .Options;
        using var db = new AgentsDbContext(dbOptions, tenantProvider);
        var approved = new TenantSkill
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "invoice-review",
            StorageKey = "approved-skill",
            ApprovalState = TenantExtensionApprovalState.Approved,
            IsActive = true,
        };
        db.TenantSkills.AddRange(approved, new TenantSkill
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "unreviewed-skill",
            StorageKey = "draft-skill",
            ApprovalState = TenantExtensionApprovalState.Draft,
            IsActive = true,
        });
        await db.SaveChangesAsync(timeout.Token);
        var fileStore = new Mock<IFileStore>(MockBehavior.Strict);
        fileStore.Setup(s => s.OpenReadAsync(approved.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(
                "---\nname: invoice-review\ndescription: Review invoice evidence.\n---\nUse authorized invoice facts.")));
        var options = new Mock<IOptionsMonitor<TenantExtensionOptions>>();
        options.SetupGet(o => o.CurrentValue).Returns(new TenantExtensionOptions());
        var logger = new CapturingLogger();
        var factory = new TenantSkillsProviderFactory(
            new TenantSkillMaterializer(NullLogger<TenantSkillMaterializer>.Instance),
            options.Object,
            logger);
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton<ITenantProvider>(tenantProvider)
            .AddSingleton(fileStore.Object)
            .BuildServiceProvider();
        string materializedDirectory = Path.Combine(
            Path.GetTempPath(), "aonik-tenant-skills", tenantId.ToString("N"));

        try
        {
            AIContextProvider? provider = factory.Create(services);
            provider.Should().NotBeNull("provider construction should succeed: {0}", logger.Error);
            using IDisposable? providerLifetime = provider as IDisposable;
            using var client = new ToolCapturingChatClient();
            var agent = new ChatClientAgent(client, new ChatClientAgentOptions
            {
                Name = "skill-compatibility",
                AIContextProviders = [provider!],
            });

            // Act
            await agent.RunAsync("Review the invoice.", cancellationToken: timeout.Token);
            AIFunction loadSkill = client.Tools.OfType<AIFunction>()
                .Single(t => t.Name == AgentSkillsProvider.LoadSkillToolName);
            object? content = await loadSkill.InvokeAsync(
                new AIFunctionArguments { ["skillName"] = approved.Name }, timeout.Token);

            // Assert
            loadSkill.Should().NotBeAssignableTo<ApprovalRequiredAIFunction>();
            client.Tools.Select(t => t.Name).Should().Contain(AgentSkillsProvider.ReadSkillResourceToolName)
                .And.NotContain(AgentSkillsProvider.RunSkillScriptToolName);
            content.Should().NotBeNull();
            content!.ToString().Should().Contain("Use authorized invoice facts.");
            fileStore.Verify(s => s.OpenReadAsync("draft-skill", It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            if (Directory.Exists(materializedDirectory))
            {
                Directory.Delete(materializedDirectory, recursive: true);
            }
        }
    }

    private sealed class CapturingLogger : ILogger<TenantSkillsProviderFactory>
    {
        public Exception? Error { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Error = exception;
    }

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;

        public bool TryGetCurrentTenantId(out Guid currentTenantId)
        {
            currentTenantId = tenantId;
            return true;
        }
    }

    private sealed class ToolCapturingChatClient : IChatClient
    {
        public IReadOnlyList<AITool> Tools { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Tools = options?.Tools?.ToList() ?? [];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Ready.")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
