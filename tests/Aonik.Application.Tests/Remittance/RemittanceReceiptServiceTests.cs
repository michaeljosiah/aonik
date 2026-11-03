using System.Text;

using Aonik.Finance.Contracts.Models.Remittance;
using Aonik.Finance.Contracts.Services.Remittance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Remittance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Aonik.Application.Tests.Remittance;

public sealed class RemittanceReceiptServiceTests
{
    [Fact]
    public async Task BouncedRecipient_Should_ReviewTheOriginalContact_AndSaveWithoutSendingOrRewritingHistory()
    {
        using var h = new Harness();
        var request = h.Request();
        await h.Service.SendAsync(h.Transfer.OrderId, request);
        await h.Delivery.RecordAsync(new("bounce", request.RequestId, request.ExpectedEmail, "Bounced", new DateTimeOffset(2026, 11, 1, 12, 1, 0, TimeSpan.Zero)));
        var auditBefore = await h.Db.OrderHistoryEvents.AsNoTracking().Select(e => e.DetailsJson).ToListAsync();
        var review = await h.Service.GetBouncedRecipientAsync(h.Transfer.OrderId, request.RequestId);
        review.Contact.ContactId.Should().Be(h.ContactId);
        review.ChangedSinceSend.Should().BeFalse();
        h.ContactEditor.Setup(c => c.UpdateEmailAsync(h.Transfer.CustomerPartyId, h.ContactId, request.ExpectedEmail, "corrected@example.test", request.RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiptEmailContact(h.ContactId, "corrected@example.test", true));

        var saved = await h.Service.UpdateBouncedRecipientAsync(h.Transfer.OrderId, request.RequestId, new(request.ExpectedEmail, "corrected@example.test", true));

        saved.Email.Should().Be("corrected@example.test");
        (await h.Db.OrderHistoryEvents.AsNoTracking().Select(e => e.DetailsJson).ToListAsync()).Should().Equal(auditBefore);
        h.Messages.Should().ContainSingle();
        h.Contacts.Setup(c => c.GetAsync(h.Transfer.CustomerPartyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerReceiptContacts("Nadia", "RemitExchange", [saved]));
        (await h.Service.GetBouncedRecipientAsync(h.Transfer.OrderId, request.RequestId)).ChangedSinceSend.Should().BeTrue();
        var staleSend = () => h.Service.SendAsync(h.Transfer.OrderId, request with { RequestId = Guid.NewGuid() });
        await staleSend.Should().ThrowAsync<InvalidStateException>();
        await h.Service.SendAsync(h.Transfer.OrderId, request with { RequestId = Guid.NewGuid(), ExpectedEmail = saved.Email });
        h.Messages.Should().HaveCount(2);
        h.Messages.Last().To.Should().Be(saved.Email);
        (await h.Service.GetEmailHistoryAsync(h.Transfer.OrderId)).Items.Single(i => i.RequestId == request.RequestId).DeliveryStatus.Should().Be("Bounced");
    }

    [Fact]
    public async Task BouncedRecipient_Should_RejectNonBouncedMissingOrUnconfirmedRequests_AndEnforcePermissions()
    {
        using var h = new Harness();
        var request = h.Request();
        await h.Service.SendAsync(h.Transfer.OrderId, request);
        var notBounced = () => h.Service.GetBouncedRecipientAsync(h.Transfer.OrderId, request.RequestId);
        await notBounced.Should().ThrowAsync<InvalidStateException>();
        var missing = () => h.Service.GetBouncedRecipientAsync(h.Transfer.OrderId, Guid.NewGuid());
        await missing.Should().ThrowAsync<NotFoundException>();
        var unconfirmed = () => h.Service.UpdateBouncedRecipientAsync(h.Transfer.OrderId, request.RequestId, new(request.ExpectedEmail, "corrected@example.test", false));
        await unconfirmed.Should().ThrowAsync<ArgumentException>();
        await h.Delivery.RecordAsync(new("bounce", request.RequestId, request.ExpectedEmail, "Bounced", new DateTimeOffset(2026, 11, 1, 12, 1, 0, TimeSpan.Zero)));
        h.Permissions.Setup(p => p.HasPermissionAsync(h.ActorId, "Customers.Write", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var denied = () => h.Service.GetBouncedRecipientAsync(h.Transfer.OrderId, request.RequestId);
        await denied.Should().ThrowAsync<PermissionDeniedException>();
        var deniedWrite = () => h.Service.UpdateBouncedRecipientAsync(h.Transfer.OrderId, request.RequestId, new(request.ExpectedEmail, "corrected@example.test", true));
        await deniedWrite.Should().ThrowAsync<PermissionDeniedException>();
        h.ContactEditor.VerifyNoOtherCalls();
        h.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task Delivery_Should_CorrelateAReservedRequest_DeduplicateReports_AndKeepAuditPrivate()
    {
        using var h = new Harness();
        var request = h.Request();
        var sent = await h.Service.SendAsync(h.Transfer.OrderId, request);
        sent.DeliveryStatus.Should().Be("AwaitingReport");
        h.Messages.Single().TrackingId.Should().Be(request.RequestId);
        var report = new ReceiptEmailDeliveryReport("provider-event-1", request.RequestId, "nadia@example.test", "Delivered", new DateTimeOffset(2026, 11, 1, 12, 1, 0, TimeSpan.Zero));

        await h.Delivery.RecordAsync(report);
        await h.Delivery.RecordAsync(report);

        var history = await h.Service.GetEmailHistoryAsync(h.Transfer.OrderId);
        history.Items.Single().DeliveryStatus.Should().Be("Delivered");
        history.Items.Single().DeliveryUpdatedAt.Should().Be(report.AttemptedAt.UtcDateTime);
        (await h.Db.OrderHistoryEvents.CountAsync()).Should().Be(3);
        (await h.Db.OrderHistoryEvents.ToListAsync()).Select(e => e.DetailsJson).Should().OnlyContain(json => !json.Contains("nadia@example.test"));
        h.Email.Verify(e => e.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delivery_Should_UseProviderTime_AndNeverReplaceATerminalResultWithExpansion()
    {
        using var h = new Harness();
        var request = h.Request();
        await h.Service.SendAsync(h.Transfer.OrderId, request);
        var origin = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        await h.Delivery.RecordAsync(new("delivered", request.RequestId, request.ExpectedEmail, "Delivered", origin.AddMinutes(2)));
        await h.Delivery.RecordAsync(new("older-bounce", request.RequestId, request.ExpectedEmail, "Bounced", origin.AddMinutes(1)));
        await h.Delivery.RecordAsync(new("expansion", request.RequestId, request.ExpectedEmail, "Expanded", origin.AddMinutes(4)));
        (await h.Service.GetSendStatusAsync(h.Transfer.OrderId, request.RequestId))!.DeliveryStatus.Should().Be("Delivered");

        await h.Delivery.RecordAsync(new("later-bounce", request.RequestId, request.ExpectedEmail, "Bounced", origin.AddMinutes(3)));
        await h.Delivery.RecordAsync(new("tied-delivered", request.RequestId, request.ExpectedEmail, "Delivered", origin.AddMinutes(3)));
        (await h.Service.GetSendStatusAsync(h.Transfer.OrderId, request.RequestId))!.DeliveryStatus.Should().Be("Bounced");
    }

    [Fact]
    public async Task Delivery_Should_ResolveAnUncertainSend_WithoutChangingTheRecordedOutcomeOrResending()
    {
        using var h = new Harness();
        h.Email.Setup(e => e.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Response lost"));
        var request = h.Request();
        (await h.Service.SendAsync(h.Transfer.OrderId, request)).Status.Should().Be("Unknown");
        await h.Delivery.RecordAsync(new("bounced", request.RequestId, request.ExpectedEmail, "Bounced", new DateTimeOffset(2026, 11, 1, 12, 1, 0, TimeSpan.Zero)));

        var replay = await h.Service.SendAsync(h.Transfer.OrderId, request);
        replay.Status.Should().Be("Accepted");
        replay.DeliveryStatus.Should().Be("Bounced");
        (await h.Service.GetEmailHistoryAsync(h.Transfer.OrderId)).Items.Single().StatusUpdatedAt.Should().Be(new DateTime(2026, 11, 1, 12, 1, 0, DateTimeKind.Utc));
        (await h.Db.OrderHistoryEvents.SingleAsync(e => e.EventType == "RemittanceReceiptEmailResult")).DetailsJson.Should().Contain("Unknown");
        h.Email.Verify(e => e.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delivery_Should_IgnoreUnmatchedRecipientsOtherTenantsAndLegacyRequests()
    {
        using var h = new Harness();
        var request = h.Request();
        await h.Service.SendAsync(h.Transfer.OrderId, request);
        var report = new ReceiptEmailDeliveryReport("event", request.RequestId, "another@example.test", "Bounced", new DateTimeOffset(2026, 11, 1, 12, 1, 0, TimeSpan.Zero));
        await h.Delivery.RecordAsync(report);
        await h.Delivery.RecordAsync(report with { MessageId = Guid.NewGuid(), Recipient = request.ExpectedEmail });
        var foreign = new ReceiptEmailDeliveryReceiver(h.Db, new TestTenantProvider(Guid.NewGuid()), h.Clock);
        await foreign.RecordAsync(report with { Recipient = request.ExpectedEmail });
        (await h.Service.GetSendStatusAsync(h.Transfer.OrderId, request.RequestId))!.DeliveryStatus.Should().Be("AwaitingReport");

        var claim = await h.Db.OrderHistoryEvents.SingleAsync(e => e.Id == request.RequestId);
        claim.DetailsJson = claim.DetailsJson.Replace(",\"DeliveryTrackingEnabled\":true", "");
        await h.Db.SaveChangesAsync();
        await h.Delivery.RecordAsync(report with { Recipient = request.ExpectedEmail });
        (await h.Service.GetSendStatusAsync(h.Transfer.OrderId, request.RequestId))!.DeliveryStatus.Should().Be("NotTracked");
        (await h.Db.OrderHistoryEvents.CountAsync()).Should().Be(2);
    }
    [Fact]
    public async Task History_Should_PageRequestsNewestFirst_AndPreserveRecipientSnapshotsWithoutResending()
    {
        using var h = new Harness();
        var first = h.Request();
        await h.Service.SendAsync(h.Transfer.OrderId, first);
        h.Email.Setup(sender => sender.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmailDeliveryNotStartedException("Not configured"));
        var second = h.Request();
        await h.Service.SendAsync(h.Transfer.OrderId, second);
        var events = await h.Db.OrderHistoryEvents.Where(e => e.Id == first.RequestId || e.Id == second.RequestId).ToListAsync();
        events.Single(e => e.Id == first.RequestId).EventAt = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        await h.Db.SaveChangesAsync();
        // Reading history needs no customer write grant or current customer contact lookup.
        h.Permissions.Setup(p => p.HasPermissionAsync(h.ActorId, "Customers.Write", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var latest = await h.Service.GetEmailHistoryAsync(h.Transfer.OrderId, 1, 1);
        var older = await h.Service.GetEmailHistoryAsync(h.Transfer.OrderId, 2, 1);

        latest.TotalCount.Should().Be(2);
        latest.Items.Should().ContainSingle().Which.RequestId.Should().Be(second.RequestId);
        latest.Items[0].Status.Should().Be("Failed");
        latest.Items[0].StatusUpdatedAt.Should().NotBeNull();
        older.Items.Should().ContainSingle().Which.RequestId.Should().Be(first.RequestId);
        older.Items[0].MaskedRecipient.Should().Be("n***@example.test");
        older.Items[0].Status.Should().Be("Accepted");
        h.Email.Verify(sender => sender.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task History_Should_ExcludeOtherTenantsOrdersAndEventTypes_AndReturnEmptyForNoRequests()
    {
        using var h = new Harness();
        var request = h.Request();
        await h.Service.SendAsync(h.Transfer.OrderId, request);
        var claim = await h.Db.OrderHistoryEvents.SingleAsync(e => e.Id == request.RequestId);
        foreach (var (tenant, order, type) in new[]
        {
            (Guid.NewGuid(), h.Transfer.OrderId, claim.EventType),
            (h.TenantId, Guid.NewGuid(), claim.EventType),
            (h.TenantId, h.Transfer.OrderId, "OtherEvent")
        }) h.Db.OrderHistoryEvents.Add(new Aonik.Finance.Entities.Orders.OrderHistoryEvent
        {
            Id = Guid.NewGuid(), TenantId = tenant, OrderId = order, EventType = type,
            EventAt = DateTime.UtcNow, DetailsJson = claim.DetailsJson, ActorType = "User", ActorId = h.ActorId
        });
        await h.Db.SaveChangesAsync();

        var history = await h.Service.GetEmailHistoryAsync(h.Transfer.OrderId);
        history.Items.Should().ContainSingle().Which.RequestId.Should().Be(request.RequestId);
        history.TotalCount.Should().Be(1);
        (await h.Service.GetEmailHistoryAsync(Guid.NewGuid())).Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Payment.Read")]
    [InlineData("Customers.Read")]
    public async Task History_Should_RejectMissingReadPermission(string missing)
    {
        using var h = new Harness();
        h.Permissions.Setup(p => p.HasPermissionAsync(h.ActorId, missing, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var act = () => h.Service.GetEmailHistoryAsync(h.Transfer.OrderId);
        await act.Should().ThrowAsync<PermissionDeniedException>();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 51)]
    [InlineData(int.MaxValue, 20)]
    public async Task History_Should_RejectUnboundedOrInvalidPagination(int page, int size)
    {
        using var h = new Harness();
        var act = () => h.Service.GetEmailHistoryAsync(h.Transfer.OrderId, page, size);
        await act.Should().ThrowAsync<ArgumentException>();
    }
    [Fact]
    public async Task Send_Should_AttachAuthoritativePdf_AndAuditWithoutRawEmail()
    {
        using var h = new Harness();
        var request = h.Request();

        var result = await h.Service.SendAsync(h.Transfer.OrderId, request);
        var replay = await h.Service.SendAsync(h.Transfer.OrderId, request);

        result.Status.Should().Be("Accepted");
        replay.Should().Be(result);
        h.Email.Verify(sender => sender.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        var sent = h.Messages.Should().ContainSingle().Which;
        sent.To.Should().Be("nadia@example.test");
        sent.TemplateName.Should().Be(TransactionalEmailTemplateNames.RemittanceReceipt);
        sent.Attachments.Should().ContainSingle().Which.Content.Should().Equal(h.Pdf);
        sent.Model["total_debit"].Should().Be("GBP 1,005.00");
        var history = await h.Db.OrderHistoryEvents.ToListAsync();
        history.Should().HaveCount(2);
        history.Should().OnlyContain(item => item.TenantId == h.TenantId && item.OrderId == h.Transfer.OrderId);
        history.Select(item => item.DetailsJson).Should().OnlyContain(json => !json.Contains("nadia@example.test"));
        history.Single(item => item.Id == request.RequestId).ActorId.Should().Be(h.ActorId);
    }

    [Theory]
    [InlineData("confirmation")]
    [InlineData("contact")]
    [InlineData("changed-email")]
    [InlineData("permission")]
    public async Task Send_Should_RejectInvalidOrUnconfirmedRecipients_BeforeDispatch(string invalid)
    {
        using var h = new Harness();
        var request = h.Request();
        request = invalid switch
        {
            "confirmation" => request with { ConfirmSend = false },
            "contact" => request with { ContactId = Guid.NewGuid() },
            "changed-email" => request with { ExpectedEmail = "substituted@example.test" },
            _ => request
        };
        if (invalid == "permission") h.Permissions.Setup(p => p.HasPermissionAsync(h.ActorId, "Customers.Write", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => h.Service.SendAsync(h.Transfer.OrderId, request);

        await act.Should().ThrowAsync<Exception>();
        h.Messages.Should().BeEmpty();
        (await h.Db.OrderHistoryEvents.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(true, "Failed")]
    [InlineData(false, "Unknown")]
    public async Task Send_Should_RecordDefinitiveFailureOrUncertainAcceptance_WithoutResending(bool definitive, string status)
    {
        using var h = new Harness();
        h.Email.Setup(sender => sender.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(definitive ? new EmailDeliveryNotStartedException("Not configured") : new IOException("Response lost"));
        var request = h.Request();

        (await h.Service.SendAsync(h.Transfer.OrderId, request)).Status.Should().Be(status);
        (await h.Service.GetEmailHistoryAsync(h.Transfer.OrderId)).Items.Should().ContainSingle().Which.Status.Should().Be(status);
        (await h.Service.GetSendStatusAsync(h.Transfer.OrderId, request.RequestId))!.Status.Should().Be(status);
        (await h.Service.SendAsync(h.Transfer.OrderId, request)).Status.Should().Be(status);

        h.Email.Verify(sender => sender.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_Should_ExposeSendingWhileProviderIsPending_AndBindReferenceToOriginalRecipient()
    {
        using var h = new Harness();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Email.Setup(sender => sender.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { started.SetResult(); await finish.Task; });
        var request = h.Request();
        var first = h.Service.SendAsync(h.Transfer.OrderId, request);
        await started.Task;

        (await h.Service.SendAsync(h.Transfer.OrderId, request)).Status.Should().Be("Sending");
        var history = await h.Service.GetEmailHistoryAsync(h.Transfer.OrderId);
        history.Items.Should().ContainSingle().Which.Status.Should().Be("Sending");
        history.Items[0].StatusUpdatedAt.Should().BeNull();
        var substitute = () => h.Service.SendAsync(h.Transfer.OrderId, request with { ExpectedEmail = "another@example.test" });
        await substitute.Should().ThrowAsync<InvalidStateException>();
        finish.SetResult();
        (await first).Status.Should().Be("Accepted");
        h.Email.Verify(sender => sender.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Receipt_Should_FailWhenTransferIsUnavailable_AndNotExposeAnotherOrdersSend()
    {
        using var h = new Harness();
        var request = h.Request();
        await h.Service.SendAsync(h.Transfer.OrderId, request);
        (await h.Service.GetSendStatusAsync(h.Transfer.OrderId, Guid.NewGuid())).Should().BeNull();
        h.Transfers.Setup(t => t.GetForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((RemittanceOrderResponse?)null);
        var act = () => h.Service.DownloadAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid ContactId { get; } = Guid.NewGuid();
        public byte[] Pdf { get; } = Encoding.ASCII.GetBytes("%PDF-authoritative-server-receipt");
        public FinanceDbContext Db { get; }
        public Mock<IOperatorRemittanceService> Transfers { get; } = new();
        public Mock<IPermissionService> Permissions { get; } = new();
        public Mock<ICustomerReceiptContactEditor> ContactEditor { get; } = new();
        public Mock<ICustomerReceiptContactReader> Contacts { get; } = new();
        public Mock<ITemplatedEmailSender> Email { get; } = new();
        public List<TemplatedEmailMessage> Messages { get; } = [];
        public RemittanceOrderResponse Transfer { get; } = new(Guid.NewGuid(), "TRF-2026-0842", "Complete", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Bank", "****4821", "GB", "NG", "GBP", "NGN", 1000m, 2050000m, 5m, 1005m, 2050m, Guid.NewGuid(), Guid.NewGuid(), "Simulated", "client-reference", "provider-reference", "Succeeded", DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
        public RemittanceReceiptService Service { get; }
        public ReceiptEmailDeliveryReceiver Delivery { get; }
        public IClock Clock { get; }

        public Harness()
        {
            var tenant = new TestTenantProvider(TenantId);
            var actor = new Mock<ICurrentUserProvider>();
            actor.Setup(user => user.GetCurrentUserId()).Returns(ActorId);
            var clock = new Mock<IClock>();clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
            Clock = clock.Object;
            Db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseInMemoryDatabase($"Receipt_{Guid.NewGuid()}").Options, tenant, actor.Object, clock.Object);
            Transfers.Setup(t => t.GetForOperatorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Transfer);
            Permissions.Setup(p => p.HasPermissionAsync(ActorId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var contacts = Contacts;
            contacts.Setup(c => c.GetAsync(Transfer.CustomerPartyId, It.IsAny<CancellationToken>())).ReturnsAsync(new CustomerReceiptContacts("Nadia Okafor", "RemitExchange", [new(ContactId, "nadia@example.test", true)]));
            var renderer = new Mock<IRemittanceReceiptRenderer>();
            renderer.Setup(r => r.Render(Transfer, "RemitExchange", TenantId, It.IsAny<DateTime>())).Returns(new RemittanceReceiptFile("transfer-receipt-TRF-2026-0842.pdf", Pdf));
            Email.Setup(e => e.SendAsync(It.IsAny<TemplatedEmailMessage>(), It.IsAny<CancellationToken>())).Callback<TemplatedEmailMessage, CancellationToken>((message, _) => Messages.Add(message)).Returns(Task.CompletedTask);
            Service = new(Db, Transfers.Object, renderer.Object, contacts.Object, ContactEditor.Object, Email.Object, tenant, actor.Object, Permissions.Object, clock.Object);
            Delivery = new(Db, tenant, clock.Object);
        }

        public SendReceiptEmailRequest Request() => new(Guid.NewGuid(), ContactId, "nadia@example.test", true);
        public void Dispose() => Db.Dispose();
    }
}
