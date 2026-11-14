using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Application.Tests.Platform.Integrations;

public class ConnectorConsentTests
{
    [Fact]
    public async Task OrganizationalReview_Should_SelectAnExistingAccount_AndGrantOnlyTheRequester()
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync(selfService: false);
        using var scope = scenario.Scope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var account = new Aonik.Platform.Entities.Integrations.ConnectorConnection { TenantId = scenario.TenantId, ConnectorKey = "gmail", Ownership = "Tenant",
            ProvisionedByUserId = scenario.ReviewerId, Status = "Active", ProviderAccountId = "org-account", GrantedScopesJson = ConnectorJson.Serialize(new[] { GmailActionKeys.ReadScope, "openid", "email" }) };
        db.ConnectorConnections.Add(account); await db.SaveChangesAsync();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
        var request = await requests.CreateAsync(scenario.Request() with { Ownership = "Tenant" });
        scenario.Current.Id = scenario.ReviewerId;

        var fulfilled = await requests.DecideAsync(request.Id, new(request.ExpectedVersion, "Approve", account.Id));

        fulfilled.Status.Should().Be("Fulfilled");
        fulfilled.ConnectionId.Should().Be(account.Id);
        var grant = await db.ConnectorConnectionGrants.SingleAsync();
        grant.GranteeType.Should().Be("User");
        grant.GranteeId.Should().Be(scenario.Current.OwnerId);
        Func<Task> managerRead = () => scope.ServiceProvider.GetRequiredService<ConnectorService>().InvokeReadAsync(account.Id, new(GmailActionKeys.Search, 1), ConnectorJson.Element(new GmailSearchRequest("invoice")));
        await managerRead.Should().ThrowAsync<Aonik.SharedKernel.Abstractions.PermissionDeniedException>();
        scenario.OAuth.Verify(provider => provider.CreateAuthorization(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Create_Should_DeduplicateExactUse_AndRejectChangedIdempotencyContent()
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync();
        using var scope = scenario.Scope();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();

        ConnectionRequestDetail first = await requests.CreateAsync(scenario.Request());
        ConnectionRequestDetail replay = await requests.CreateAsync(scenario.Request());
        ConnectionRequestDetail equivalent = await requests.CreateAsync(scenario.Request("another-key") with { Purpose = "Same access for invoice review" });
        Func<Task> changed = () => requests.CreateAsync(scenario.Request() with { Purpose = "Changed content" });

        replay.Id.Should().Be(first.Id);
        equivalent.Id.Should().Be(first.Id);
        await changed.Should().ThrowAsync<ConnectorAccessException>().Where(error => error.Code == "Conflict");
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await db.ConnectorConnectionRequests.CountAsync()).Should().Be(1);
        (await db.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().CountAsync()).Should().Be(2);
        (await db.ConnectorConnectionGrants.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UnknownConnector_Should_RecordDesiredCapability_WithoutInventingScopes()
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync();
        using var scope = scenario.Scope();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();

        var detail = await requests.CreateAsync(scenario.Request() with { ConnectorKey = "future-crm", Actions = [], DesiredCapability = "Read customer invoice contacts" });

        detail.Status.Should().Be("Pending");
        detail.RequiredScopes.Should().BeEmpty();
        detail.DesiredCapability.Should().Be("Read customer invoice contacts");
        detail.CanConnect.Should().BeFalse();
        scenario.OAuth.Verify(provider => provider.CreateAuthorization(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Review_Should_NotProvisionPolicyOrProvider(bool enabled, bool provisioned)
    {
        using var scenario = new ConnectorScenario { Provisioned = provisioned };
        await scenario.InitializeAsync(enabled, false);
        using var scope = scenario.Scope();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
        var request = await requests.CreateAsync(scenario.Request());
        scenario.Current.Id = scenario.ReviewerId;

        var reviewed = await requests.DecideAsync(request.Id, new(request.ExpectedVersion, "Approve"));

        reviewed.Status.Should().Be("Pending");
        (await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().ConnectorConnectionGrants.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Callback_Should_StageOnly_AndOwnerConfirmationShouldGrantExactUseOnce()
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync();
        using var scope = scenario.Scope();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
        var consent = scope.ServiceProvider.GetRequiredService<ConnectorConsentService>();
        var request = await requests.CreateAsync(scenario.Request());
        var challenge = await consent.BeginAsync(request.Id, new(request.ExpectedVersion));

        await consent.ReceiveCallbackAsync(challenge.Cookie, ConnectorScenario.State(challenge), "code", scenario.Current.OwnerId);

        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await db.ConnectorConnections.SingleAsync()).Status.Should().Be("PendingConsent");
        (await db.ConnectorConnectionGrants.CountAsync()).Should().Be(0);
        var staged = (await requests.GetAsync(request.Id))!;
        staged.Status.Should().Be("AwaitingConsent");
        staged.ConsentedAccountLabel.Should().Be(scenario.Account.Label);
        var confirmed = await consent.ConfirmAsync(request.Id, new(staged.ExpectedVersion));
        var replay = await consent.ConfirmAsync(request.Id, new(staged.ExpectedVersion));

        confirmed.Status.Should().Be("Fulfilled");
        replay.GrantId.Should().Be(confirmed.GrantId);
        var connection = await db.ConnectorConnections.SingleAsync();
        connection.Status.Should().Be("Active");
        connection.ProtectedCredentials.Should().NotContain("synthetic-access");
        var grant = await db.ConnectorConnectionGrants.SingleAsync();
        grant.GranteeId.Should().Be(scenario.Current.OwnerId);
        ConnectorJson.Read<List<ConnectorActionRef>>(grant.AllowedActionsJson).Should().Equal(scenario.Request().Actions);
        (await db.ConnectorConnectionRequests.SingleAsync()).ProtectedOAuthResult.Should().BeNull();
        Func<Task> callbackReplay = () => consent.ReceiveCallbackAsync(challenge.Cookie, ConnectorScenario.State(challenge), "code", scenario.Current.OwnerId);
        await callbackReplay.Should().ThrowAsync<ConnectorAccessException>();
        scenario.OAuth.Verify(provider => provider.ExchangeAsync("code", "synthetic-verifier", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("state")]
    [InlineData("user")]
    [InlineData("expired")]
    [InlineData("cancelled")]
    public async Task Callback_Should_RefuseInvalidConsent(string failure)
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync();
        using var scope = scenario.Scope();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
        var consent = scope.ServiceProvider.GetRequiredService<ConnectorConsentService>();
        var request = await requests.CreateAsync(scenario.Request());
        var challenge = await consent.BeginAsync(request.Id, new(request.ExpectedVersion));
        if (failure == "expired") scenario.Clock.UtcNow = challenge.ExpiresAt;
        if (failure == "cancelled") await requests.CancelAsync(request.Id, new((await requests.GetAsync(request.Id))!.ExpectedVersion));

        Func<Task> callback = () => consent.ReceiveCallbackAsync(challenge.Cookie, failure == "state" ? "wrong-state" : ConnectorScenario.State(challenge),
            "code", failure == "user" ? scenario.OutsiderId : scenario.Current.OwnerId);

        await callback.Should().ThrowAsync<Exception>();
        (await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().ConnectorConnectionGrants.CountAsync()).Should().Be(0);
        scenario.OAuth.Verify(provider => provider.ExchangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_Should_UseActualProviderScopes()
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync();
        scenario.Tokens = scenario.Tokens with { GrantedScopes = ["openid", "email"] };
        using var scope = scenario.Scope();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
        var consent = scope.ServiceProvider.GetRequiredService<ConnectorConsentService>();
        var request = await requests.CreateAsync(scenario.Request());
        var challenge = await consent.BeginAsync(request.Id, new(request.ExpectedVersion));
        await consent.ReceiveCallbackAsync(challenge.Cookie, ConnectorScenario.State(challenge), "code", scenario.Current.OwnerId);

        Func<Task> confirm = async () => await consent.ConfirmAsync(request.Id, new((await requests.GetAsync(request.Id))!.ExpectedVersion));

        await confirm.Should().ThrowAsync<ConnectorAccessException>().Where(error => error.Code == "InsufficientScopes");
        (await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().ConnectorConnectionGrants.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CancellationDuringCodeExchange_Should_PreventApplyingResult()
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync();
        using var scope = scenario.Scope();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
        var consent = scope.ServiceProvider.GetRequiredService<ConnectorConsentService>();
        var request = await requests.CreateAsync(scenario.Request());
        var challenge = await consent.BeginAsync(request.Id, new(request.ExpectedVersion));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokens = new TaskCompletionSource<ConnectorOAuthTokens>(TaskCreationOptions.RunContinuationsAsynchronously);
        scenario.OAuth.Setup(provider => provider.ExchangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => { entered.SetResult(); return tokens.Task; });
        Task<Guid> callback = consent.ReceiveCallbackAsync(challenge.Cookie, ConnectorScenario.State(challenge), "code", scenario.Current.OwnerId);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using (var other = scenario.Scope())
        {
            var service = other.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
            var current = (await service.GetAsync(request.Id))!;
            await service.CancelAsync(request.Id, new(current.ExpectedVersion));
        }
        tokens.SetResult(scenario.Tokens);

        Func<Task> finish = async () => await callback;
        await finish.Should().ThrowAsync<ConnectorAccessException>().Where(error => error.Code == "Conflict");
        using var final = scenario.Scope();
        var db = final.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await db.ConnectorConnectionRequests.SingleAsync()).Status.Should().Be("Cancelled");
        (await db.ConnectorConnections.SingleAsync()).Status.Should().Be("Revoked");
        (await db.ConnectorConnectionGrants.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Expiry_Should_ClearConsent_AndRetainOnlyEligibleRequest(bool consentOnly)
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync();
        using var scope = scenario.Scope();
        var requests = scope.ServiceProvider.GetRequiredService<ConnectorConnectionRequestService>();
        var consent = scope.ServiceProvider.GetRequiredService<ConnectorConsentService>();
        var request = await requests.CreateAsync(scenario.Request());
        var challenge = await consent.BeginAsync(request.Id, new(request.ExpectedVersion));
        await consent.ReceiveCallbackAsync(challenge.Cookie, ConnectorScenario.State(challenge), "code", scenario.Current.OwnerId);
        scenario.Clock.UtcNow = consentOnly ? challenge.ExpiresAt : request.ExpiresAt;
        var context = new TaskActionContext(scenario.TenantId, Guid.NewGuid(), Guid.NewGuid(), TaskKinds.ScheduledAction,
            "ConnectorConnectionRequest", request.Id, TaskAssigneeTypes.System, null, null, scenario.Clock.UtcNow,
            ConnectorJson.Serialize(new ConnectorRequestExpiryPayload(request.Id, consentOnly)));

        var result = await scope.ServiceProvider.GetRequiredService<ConnectorRequestExpiryHandler>().ExecuteAsync(context);

        result.Outcome.Should().Be(TaskActionOutcome.Succeeded);
        var entity = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().ConnectorConnectionRequests.SingleAsync();
        entity.Status.Should().Be(consentOnly ? "AwaitingConsent" : "Expired");
        entity.ProtectedOAuthResult.Should().BeNull();
        entity.ProtectedVerifier.Should().BeNull();
    }
}
