using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.GiftCards;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Events.Integration;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Aonik.Application.Tests.Commerce;

public sealed class GiftCardDeliveryTests
{
    [Fact]
    public async Task Stage_Should_PreserveOneExactPurchase_AndRejectChangedReplay()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        await GiftCardDeliveryData.StageTrackedAsync(fixture.Db, fixture.TenantId, fixture.Cart.Id,
            fixture.OrderId, fixture.PaymentId, fixture.Purchase);
        await fixture.Db.SaveChangesAsync();
        (await fixture.Db.OrderGiftCardDeliveries.CountAsync()).Should().Be(1);
        var changed = () => GiftCardDeliveryData.StageTrackedAsync(fixture.Db, fixture.TenantId, fixture.Cart.Id,
            fixture.OrderId, fixture.PaymentId, fixture.Purchase with { RecipientEmail = "changed@example.test" });
        await changed.Should().ThrowAsync<InvalidStateException>();
        (await fixture.RowAsync()).GiftCardId.Should().BeNull();
        fixture.Scheduled.Should().BeEmpty();
    }

    [Fact]
    public async Task Issuance_Should_ValidateExactSourceAndMoney_ThenResumeTheSameScheduledTask()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        var wrongSource = () => fixture.Service().ActivateAsync(fixture.Event with
        { Source = fixture.Source with { OrderItemId = Guid.NewGuid() } });
        await wrongSource.Should().ThrowAsync<InvalidStateException>();
        fixture.Card = fixture.Card with { FaceValue = 99 };
        var wrongAmount = () => fixture.Service().ActivateAsync(fixture.Event);
        await wrongAmount.Should().ThrowAsync<InvalidStateException>();
        fixture.Card = fixture.Card with { FaceValue = fixture.Purchase.FaceValue };
        // SQL datetime2 materializes Unspecified; stable scheduling requires an explicit UTC instant.
        var stored = await fixture.Db.OrderGiftCardDeliveries.SingleAsync();
        stored.SendAtUtc = DateTime.SpecifyKind(stored.SendAtUtc!.Value, DateTimeKind.Unspecified);
        await fixture.Db.SaveChangesAsync();

        await fixture.Service().ActivateAsync(fixture.Event);
        await fixture.Service().ActivateAsync(fixture.Event);

        fixture.Scheduled.Should().HaveCount(2);
        fixture.Scheduled.Select(request => request.TaskId).Distinct().Should().ContainSingle();
        fixture.Scheduled.Should().OnlyContain(request => request.RunAtUtc == fixture.Purchase.SendAtUtc);
        fixture.Scheduled.Should().OnlyContain(request => request.RunAtUtc!.Value.Kind == DateTimeKind.Utc);
        var payload = fixture.Scheduled[0].ActionPayloadJson;
        payload.Should().NotContain("recipient").And.NotContain(Fixture.Code).And.NotContain("example.test");
        (await fixture.RowAsync()).SendSequence.Should().Be(1);
    }

    [Fact]
    public async Task Task_Should_WaitForFrozenDueTime_AndSubmitOnceAfterSuccessfulRecordedSequence()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        await fixture.Service().ActivateAsync(fixture.Event);
        var context = await fixture.TaskAsync();
        (await fixture.Service().SendAsync(context, fixture.DeliveryId, 1)).Outcome.Should().Be(TaskActionOutcome.Failed);
        fixture.Messages.Should().BeEmpty();
        fixture.Clock.UtcNow = fixture.Purchase.SendAtUtc!.Value;
        fixture.Cart.CheckoutDraftJson = "{\"recipientEmail\":\"late-change@example.test\"}";
        await fixture.Db.SaveChangesAsync();

        (await fixture.Service().SendAsync(context, fixture.DeliveryId, 1)).Outcome.Should().Be(TaskActionOutcome.Succeeded);
        (await fixture.Service().SendAsync(context, fixture.DeliveryId, 1)).Outcome.Should().Be(TaskActionOutcome.Succeeded);

        var message = fixture.Messages.Should().ContainSingle().Which;
        message.To.Should().Be("recipient@example.test");
        message.Model["gift_code"].Should().Be(Fixture.Code);
        message.Model["message"].Should().Be("Enjoy <your gift>");
        (await fixture.RowAsync()).SentSequence.Should().Be(1);
    }

    [Fact]
    public async Task FailedSubmission_Should_RedactProviderSecrets_AndRetryTheSameInstrument()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        await fixture.Service().ActivateAsync(fixture.Event);
        fixture.Clock.UtcNow = fixture.Purchase.SendAtUtc!.Value;
        fixture.FailEmail = true;
        var handler = new GiftCardDeliveryTaskHandler(fixture.Service());
        var failed = await handler.ExecuteAsync(await fixture.TaskAsync());
        failed.Outcome.Should().Be(TaskActionOutcome.Failed);
        JsonSerializer.Serialize(failed).Should().NotContain(Fixture.Code).And.NotContain("recipient@example.test");
        (await fixture.RowAsync()).SentSequence.Should().Be(0);
        fixture.FailEmail = false;

        (await handler.ExecuteAsync(await fixture.TaskAsync())).Outcome.Should().Be(TaskActionOutcome.Succeeded);

        fixture.Messages.Should().HaveCount(2).And.OnlyContain(message => Equals(message.Model["gift_code"], Fixture.Code));
        fixture.Cards.Verify(service => service.GetFulfilmentSecretAsync(fixture.Source, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Resend_Should_KeepRecipientAndCode_AndEnforceDurableCooldownAndBudget()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        await fixture.Service().ActivateAsync(fixture.Event);
        fixture.Clock.UtcNow = fixture.Purchase.SendAtUtc!.Value;
        await fixture.Service().SendAsync(await fixture.TaskAsync(), fixture.DeliveryId, 1);
        var early = () => fixture.Service().RequestResendAsync(fixture.DeliveryId, fixture.PartyId);
        await early.Should().ThrowAsync<InvalidStateException>();
        for (var sequence = 2; sequence <= 4; sequence++)
        {
            fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(5);
            await fixture.Service().RequestResendAsync(fixture.DeliveryId, fixture.PartyId);
            // Repeating while scheduled resumes this intent and never creates another sequence.
            await fixture.Service().RequestResendAsync(fixture.DeliveryId, fixture.PartyId);
            (await fixture.RowAsync()).SendSequence.Should().Be(sequence);
            await fixture.Service().SendAsync(await fixture.TaskAsync(), fixture.DeliveryId, sequence);
        }
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(5);
        var exceeded = () => fixture.Service().RequestResendAsync(fixture.DeliveryId, fixture.PartyId);
        await exceeded.Should().ThrowAsync<InvalidStateException>();
        fixture.Messages.Should().HaveCount(4).And.OnlyContain(message => message.To == "recipient@example.test"
            && Equals(message.Model["gift_code"], Fixture.Code));
        fixture.Scheduled.Select(request => request.TaskId).Distinct().Should().HaveCount(4);
        (await fixture.Service().ListSentAsync(fixture.PartyId)).Items.Single().CanResend.Should().BeFalse();
    }

    [Fact]
    public async Task ListAndResend_Should_UseCurrentCartOwner_AndExposeOnlyMaskedDeliveryFacts()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        await fixture.Service().ActivateAsync(fixture.Event);
        var otherParty = Guid.NewGuid();
        (await fixture.Service().ListSentAsync(otherParty)).Items.Should().BeEmpty();
        var foreign = () => fixture.Service().RequestResendAsync(fixture.DeliveryId, otherParty);
        await foreign.Should().ThrowAsync<NotFoundException>();
        var listed = await fixture.Service().ListSentAsync(fixture.PartyId);
        var json = JsonSerializer.Serialize(listed);
        json.Should().NotContain(Fixture.Code).And.NotContain("recipient@example.test").And.NotContain("Enjoy <your gift>")
            .And.NotContain("PurchaseSnapshotJson").And.NotContain("PaymentIntentId");
        listed.Items.Single().MaskedRecipientEmail.Should().Be("***@example.test");
        fixture.Cart.BuyerPartyId = otherParty;
        await fixture.Db.SaveChangesAsync();
        (await fixture.Service().ListSentAsync(fixture.PartyId)).Items.Should().BeEmpty();
        (await fixture.Service().ListSentAsync(otherParty)).Items.Should().ContainSingle();
    }

    [Theory]
    [InlineData(GiftCardDeliveryMethods.Post, "Posted")]
    [InlineData(GiftCardDeliveryMethods.InFoodBox, "Enclosed")]
    public async Task Physical_Should_BePrivate_ReadWithoutMutation_AndRecordPrintThenVersionedCompletion(string method, string completed)
    {
        using var fixture = new Fixture(method);
        await fixture.SeedAsync();
        await fixture.Service().ActivateAsync(fixture.Event);
        fixture.Scheduled.Should().BeEmpty();
        var before = await fixture.RowAsync();
        var listing = await fixture.Service().ListPhysicalAsync();
        JsonSerializer.Serialize(listing).Should().NotContain(Fixture.Code).And.NotContain("Street");
        (await fixture.RowAsync()).LastPrintedAtUtc.Should().BeNull();
        (await fixture.RowAsync()).CompletedAtUtc.Should().BeNull();
        var unprinted = () => fixture.Service().CompletePhysicalAsync(fixture.DeliveryId, listing.Items.Single().Version);
        await unprinted.Should().ThrowAsync<InvalidStateException>();

        var print = await fixture.Service().PrintAsync(fixture.DeliveryId);
        print.Code.Should().Be(Fixture.Code);
        print.RecipientName.Should().Be("Recipient");
        (await fixture.RowAsync()).LastPrintedBy.Should().Be(fixture.ActorId);
        (await fixture.RowAsync()).CompletedAtUtc.Should().BeNull();
        var stale = () => fixture.Service().CompletePhysicalAsync(fixture.DeliveryId, Convert.ToBase64String([99]));
        await stale.Should().ThrowAsync<DbUpdateConcurrencyException>();
        var result = await fixture.Service().CompletePhysicalAsync(fixture.DeliveryId, print.Version);
        result.Status.Should().Be(completed);
        var replay = await fixture.Service().CompletePhysicalAsync(fixture.DeliveryId, print.Version);
        replay.CompletedAtUtc.Should().Be(result.CompletedAtUtc);
        (await fixture.RowAsync()).CompletedBy.Should().Be(fixture.ActorId);
        (await fixture.Service().ListPhysicalAsync()).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task PubliclyScheduledTask_Should_NotObtainCodeOrSendEvenWithKnownDeliveryId()
    {
        using var fixture = new Fixture();
        await fixture.SeedAsync();
        await fixture.Service().ActivateAsync(fixture.Event);
        fixture.Clock.UtcNow = fixture.Purchase.SendAtUtc!.Value;
        var forged = (await fixture.TaskAsync()) with { WorkItemId = Guid.NewGuid() };
        (await new GiftCardDeliveryTaskHandler(fixture.Service()).ExecuteAsync(forged)).Outcome.Should().Be(TaskActionOutcome.Failed);
        fixture.Cards.Verify(service => service.GetFulfilmentSecretAsync(It.IsAny<GiftCardPurchaseSource>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedPrintSave_Should_DetachOnlyFailedDelivery_AndNeverReturnSecret()
    {
        using var fixture = new Fixture(GiftCardDeliveryMethods.Post);
        await fixture.SeedAsync();
        await fixture.Service().ActivateAsync(fixture.Event);
        var fail = new FailOnce();
        await using var context = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>(fixture.Options)
            .AddInterceptors(fail).Options, new TestTenantProvider(fixture.TenantId));
        var print = () => fixture.Service(context).PrintAsync(fixture.DeliveryId);
        await print.Should().ThrowAsync<DbUpdateException>();
        context.ChangeTracker.Entries<OrderGiftCardDelivery>().Should().BeEmpty();
        var cart = await context.Carts.SingleAsync();
        cart.CheckoutDraftJson = "{}";
        await context.SaveChangesAsync();
        (await fixture.RowAsync()).LastPrintedAtUtc.Should().BeNull();
    }

    private sealed class FailOnce : SaveChangesInterceptor
    {
        private bool failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!failed) { failed = true; throw new DbUpdateException("Simulated rejected write."); }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Clock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Code = "SECRET-GIFT-CODE-42";
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid OrderId { get; } = Guid.NewGuid();
        public Guid PaymentId { get; } = Guid.NewGuid();
        public Guid PartyId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid DeliveryId { get; private set; }
        public Clock Clock { get; } = new();
        public DbContextOptions<CommerceDbContext> Options { get; } = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"GiftCardDelivery_{Guid.NewGuid()}").Options;
        public CommerceDbContext Db { get; }
        public Cart Cart { get; }
        public GiftCardPurchaseSnapshot Purchase { get; }
        public GiftCardPurchaseSource Source => new(Cart.Id, OrderId, PaymentId, Purchase.OrderItemId, Purchase.ItemIndex);
        public GiftCardIssuedInfo Card { get; set; }
        public GiftCardIssuedEvent Event => new(TenantId, Card.GiftCardId, Source);
        public Mock<IGiftCardService> Cards { get; } = new(MockBehavior.Strict);
        public Mock<ITaskService> Tasks { get; } = new(MockBehavior.Strict);
        public Mock<ITemplatedEmailSender> Email { get; } = new();
        public Mock<ICurrentUserProvider> User { get; } = new();
        public Mock<IPermissionService> Permissions { get; } = new();
        public List<ScheduleTaskRequest> Scheduled { get; } = [];
        public List<TemplatedEmailMessage> Messages { get; } = [];
        public bool FailEmail { get; set; }

        public Fixture(string method = GiftCardDeliveryMethods.Email)
        {
            Db = new(Options, new TestTenantProvider(TenantId), clock: Clock);
            Cart = new() { TenantId = TenantId, OrderId = OrderId, BuyerPartyId = PartyId, Currency = "GBP", Status = CartStatuses.CheckedOut };
            Purchase = new(1, Guid.NewGuid(), 25m, "GBP", method, "Recipient",
                method == GiftCardDeliveryMethods.Email ? "recipient@example.test" : null,
                method == GiftCardDeliveryMethods.Email ? Clock.UtcNow.AddHours(2) : null,
                method == GiftCardDeliveryMethods.Post ? new DateOnly(2026, 10, 10) : null,
                method == GiftCardDeliveryMethods.Post ? new("1 Street", null, "London", null, "SW1A 1AA", "GB") : null,
                Message: "Enjoy <your gift>", SenderName: "Purchaser");
            Card = new(Guid.NewGuid(), Source, Purchase.FaceValue, "GBP", "****-0042", Clock.UtcNow, null, "terms-v1", "Active");
            Cards.Setup(service => service.GetIssuedAsync(Source, It.IsAny<CancellationToken>())).ReturnsAsync(() => Card);
            Cards.Setup(service => service.GetFulfilmentSecretAsync(Source, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new GiftCardFulfilmentSecret(Card, Code));
            Tasks.Setup(service => service.ScheduleAsync(It.IsAny<ScheduleTaskRequest>(), It.IsAny<CancellationToken>()))
                .Callback<ScheduleTaskRequest, CancellationToken>((request, _) => Scheduled.Add(request)).ReturnsAsync((TaskResponse)null!);
            Tasks.Setup(service => service.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((TaskResponse?)null);
            Email.Setup(service => service.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()))
                .Returns<TemplatedEmailMessage, CancellationToken>((message, _) =>
                {
                    Messages.Add(message);
                    return FailEmail ? Task.FromException(new InvalidOperationException($"Provider rejected {message.To}: {Code}")) : Task.CompletedTask;
                });
            var actor = ActorId;
            User.Setup(user => user.TryGetCurrentUserId(out actor)).Returns(true);
            Permissions.Setup(service => service.HasPermissionAsync(actor, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        public async Task SeedAsync()
        {
            Db.Carts.Add(Cart);
            await GiftCardDeliveryData.StageTrackedAsync(Db, TenantId, Cart.Id, OrderId, PaymentId, Purchase);
            await Db.SaveChangesAsync();
            DeliveryId = (await Db.OrderGiftCardDeliveries.SingleAsync()).Id;
        }

        public GiftCardDeliveryService Service(CommerceDbContext? context = null) => new(context ?? Db,
            new TestTenantProvider(TenantId), Clock, Cards.Object, Tasks.Object, Email.Object, User.Object, Permissions.Object);
        public Task<OrderGiftCardDelivery> RowAsync() => Db.OrderGiftCardDeliveries.AsNoTracking().SingleAsync();
        public async Task<TaskActionContext> TaskAsync()
        {
            var row = await RowAsync();
            return new(TenantId, GiftCardDeliveryData.TaskId(TenantId, row.Id, row.SendSequence), Guid.NewGuid(),
                "ScheduledAction", GiftCardDeliveryService.SubjectType, row.Id, "System", null, null,
                row.SequenceDueAtUtc!.Value, JsonSerializer.Serialize(new GiftCardDeliveryTaskPayload(row.Id, row.SendSequence)));
        }
        public void Dispose() => Db.Dispose();
    }
}
