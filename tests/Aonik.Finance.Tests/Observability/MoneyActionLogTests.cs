using Aonik.Finance.Services.Observability;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Tests.Observability;

/// <summary>
/// Locks down the structured-logging contract for <see cref="MoneyActionLog"/>
/// and <see cref="OrderLogScope"/> — the rails that GitHub Issue #142
/// depends on. The saved KQL query at
/// <c>docs/observability/queries/money-action-by-orderid.kql</c> pivots
/// on customDimensions keys named "OrderId", "Stage", "Outcome",
/// "EventId", "PricingQuoteId", "PaymentIntentId", and "InvoiceId".
/// If a property name drifts or an EventId/EventName changes here, the
/// KQL query goes silent without an error — these assertions catch
/// the drift at PR time.
/// </summary>
public class MoneyActionLogTests
{
    // ── EventName + EventId schema -------------------------------------

    [Fact]
    public void QuoteCreated_Emits_MoneyAction_EventName_With_EventId_1101()
    {
        var (logger, sink) = CreateCapturingLogger();

        logger.QuoteCreated(
            pricingQuoteId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            action: "BillPayment USD->KES",
            amount: 100m,
            currency: "USD");

        var entry = sink.Single();
        entry.EventId.Id.Should().Be(1101);
        entry.EventId.Name.Should().Be("MoneyAction");
        entry.Level.Should().Be(LogLevel.Information);
    }

    [Fact]
    public void All_Lifecycle_Events_Share_EventName_MoneyAction()
    {
        var (logger, sink) = CreateCapturingLogger();
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var journalEntryId = Guid.NewGuid();

        logger.QuoteCreated(Guid.NewGuid(), tenantId, "act", 100m, "USD");
        logger.QuoteFailed(null, tenantId, "act", "boom");
        logger.OrderConfirmed(orderId, tenantId, "act");
        logger.OrderRejected(orderId, tenantId, "boom");
        logger.PaymentCaptured(orderId, tenantId, paymentIntentId, 100m, "USD");
        logger.PaymentCaptureFailed(orderId, tenantId, paymentIntentId, "boom");
        logger.PaymentCaptureSkippedIdempotent(orderId, tenantId, paymentIntentId);
        logger.PaymentTransmitted(orderId, tenantId, "Stripe", "pi_abc");
        logger.PaymentTransmitFailed(orderId, tenantId, "Stripe", "boom");
        logger.PaymentTransmitTimeout(orderId, tenantId, "Stripe", 30000L);
        logger.LedgerPosted(orderId, tenantId, journalEntryId, 100m, "USD");
        logger.LedgerPostSkippedIdempotent(orderId, tenantId);
        logger.LedgerPostFailed(orderId, tenantId, "boom");
        logger.WebhookReceived(orderId, tenantId, "Stripe", "charge.captured");
        logger.WebhookProcessed(orderId, tenantId, "Stripe", "charge.captured", "success");
        logger.WebhookRejected(orderId, tenantId, "Stripe", "boom");

        sink.Should().HaveCount(16);
        sink.Should().OnlyContain(e => e.EventId.Name == "MoneyAction",
            "the saved KQL query filters on customDimensions.EventName == \"MoneyAction\"; drift breaks the query silently");
    }

    [Fact]
    public void EventIds_Are_Allocated_Per_Stage_Band()
    {
        // 11xx Quote, 12xx Confirm, 13xx Capture, 14xx Transmit, 15xx Settle, 16xx Webhook.
        // The runbook + saved KQL filter on EventId bands to group by stage,
        // so the per-stage range MUST stay stable.
        var (logger, sink) = CreateCapturingLogger();
        var t = Guid.NewGuid();
        var o = Guid.NewGuid();

        logger.QuoteCreated(Guid.NewGuid(), t, "act", 1m, "USD");      // 1101
        logger.OrderConfirmed(o, t, "act");                              // 1201
        logger.PaymentCaptured(o, t, Guid.NewGuid(), 1m, "USD");        // 1301
        logger.PaymentTransmitted(o, t, "Stripe", "x");                  // 1401
        logger.LedgerPosted(o, t, Guid.NewGuid(), 1m, "USD");           // 1501
        logger.WebhookReceived(o, t, "Stripe", "x");                     // 1601

        sink[0].EventId.Id.Should().BeInRange(1100, 1199);
        sink[1].EventId.Id.Should().BeInRange(1200, 1299);
        sink[2].EventId.Id.Should().BeInRange(1300, 1399);
        sink[3].EventId.Id.Should().BeInRange(1400, 1499);
        sink[4].EventId.Id.Should().BeInRange(1500, 1599);
        sink[5].EventId.Id.Should().BeInRange(1600, 1699);
    }

    // ── Structured property contract -----------------------------------

    [Fact]
    public void PaymentCaptured_Emits_OrderId_PaymentIntentId_Amount_Currency()
    {
        var (logger, sink) = CreateCapturingLogger();
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();

        logger.PaymentCaptured(orderId, tenantId, paymentIntentId, 100.50m, "USD");

        var entry = sink.Single();
        entry.State.Should().ContainKey("OrderId").WhoseValue.Should().Be(orderId);
        entry.State.Should().ContainKey("PaymentIntentId").WhoseValue.Should().Be(paymentIntentId);
        entry.State.Should().ContainKey("Amount").WhoseValue.Should().Be(100.50m);
        entry.State.Should().ContainKey("Currency").WhoseValue.Should().Be("USD");
    }

    [Fact]
    public void OrderConfirmed_Carries_PricingQuoteId_As_Join_Key()
    {
        // The Confirm-stage log is the join point — it must carry BOTH
        // OrderId AND PricingQuoteId so KQL can chain back to the
        // Quote stage (where logs only have PricingQuoteId).
        var (logger, sink) = CreateCapturingLogger();
        var orderId = Guid.NewGuid();
        var pricingQuoteId = Guid.NewGuid();

        logger.OrderConfirmed(orderId, Guid.NewGuid(), "Status=Submitted", pricingQuoteId);

        var entry = sink.Single();
        entry.State.Should().ContainKey("OrderId").WhoseValue.Should().Be(orderId);
        entry.State.Should().ContainKey("PricingQuoteId").WhoseValue.Should().Be(pricingQuoteId);
    }

    [Fact]
    public void Stage_And_Outcome_Are_Emitted_As_Scope_Properties_Not_State()
    {
        // Stage and Outcome come from BeginScope, not from the LoggerMessage
        // template — that's how the same EventName carries different
        // (Stage, Outcome) combinations queryably.
        var (logger, sink) = CreateCapturingLogger();

        logger.PaymentCaptured(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, "USD");

        var entry = sink.Single();
        entry.Scopes.Should().ContainKey("Stage").WhoseValue.Should().Be(MoneyActionStages.Capture);
        entry.Scopes.Should().ContainKey("Outcome").WhoseValue.Should().Be(MoneyActionOutcomes.Success);
    }

    [Fact]
    public void Outcomes_Map_To_The_Closed_Set()
    {
        var (logger, sink) = CreateCapturingLogger();
        var t = Guid.NewGuid();
        var o = Guid.NewGuid();
        var p = Guid.NewGuid();

        logger.PaymentCaptured(o, t, p, 1m, "USD");
        logger.PaymentCaptureFailed(o, t, p, "boom");
        logger.PaymentCaptureSkippedIdempotent(o, t, p);
        logger.PaymentTransmitTimeout(o, t, "Stripe", 1L);
        logger.OrderRejected(o, t, "boom");

        sink[0].Scopes["Outcome"].Should().Be(MoneyActionOutcomes.Success);
        sink[1].Scopes["Outcome"].Should().Be(MoneyActionOutcomes.Failed);
        sink[2].Scopes["Outcome"].Should().Be(MoneyActionOutcomes.SkippedIdempotent);
        sink[3].Scopes["Outcome"].Should().Be(MoneyActionOutcomes.Timeout);
        sink[4].Scopes["Outcome"].Should().Be(MoneyActionOutcomes.Rejected);
    }

    // ── BeginOrderScope contract ---------------------------------------

    [Fact]
    public void BeginOrderScope_Pushes_OrderId_Into_Active_Scope()
    {
        var (logger, sink) = CreateCapturingLogger();
        var orderId = Guid.NewGuid();

        using (logger.BeginOrderScope(orderId))
        {
            logger.LogInformation("inside scope");
        }
        logger.LogInformation("outside scope");

        sink.Should().HaveCount(2);
        sink[0].Scopes.Should().ContainKey("OrderId").WhoseValue.Should().Be(orderId);
        sink[1].Scopes.Should().NotContainKey("OrderId");
    }

    [Fact]
    public void BeginOrderScope_With_Optional_Ids_Pushes_All_Three()
    {
        var (logger, sink) = CreateCapturingLogger();
        var orderId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        using (logger.BeginOrderScope(orderId, paymentIntentId, invoiceId))
        {
            logger.LogInformation("with all ids");
        }

        var entry = sink.Single();
        entry.Scopes.Should().ContainKey("OrderId").WhoseValue.Should().Be(orderId);
        entry.Scopes.Should().ContainKey("PaymentIntentId").WhoseValue.Should().Be(paymentIntentId);
        entry.Scopes.Should().ContainKey("InvoiceId").WhoseValue.Should().Be(invoiceId);
    }

    [Fact]
    public void BeginOrderScope_With_Null_Optionals_Only_Pushes_OrderId()
    {
        var (logger, sink) = CreateCapturingLogger();
        var orderId = Guid.NewGuid();

        using (logger.BeginOrderScope(orderId, paymentIntentId: null, invoiceId: null))
        {
            logger.LogInformation("order only");
        }

        var entry = sink.Single();
        entry.Scopes.Should().ContainKey("OrderId");
        entry.Scopes.Should().NotContainKey("PaymentIntentId");
        entry.Scopes.Should().NotContainKey("InvoiceId");
    }

    // ── Test plumbing --------------------------------------------------

    private static (ILogger<MoneyActionLogTests> Logger, List<CapturedEntry> Sink) CreateCapturingLogger()
    {
        var sink = new List<CapturedEntry>();
        var factory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CapturingLoggerProvider(sink)));
        return (factory.CreateLogger<MoneyActionLogTests>(), sink);
    }

    private sealed record CapturedEntry(
        LogLevel Level,
        EventId EventId,
        string Message,
        IReadOnlyDictionary<string, object?> State,
        IReadOnlyDictionary<string, object?> Scopes);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<CapturedEntry> _sink;
        public CapturingLoggerProvider(List<CapturedEntry> sink) => _sink = sink;
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_sink);
        public void Dispose() { }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly List<CapturedEntry> _sink;
        private readonly AsyncLocal<Stack<IDictionary<string, object?>>> _scopes = new();
        public CapturingLogger(List<CapturedEntry> sink) => _sink = sink;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            _scopes.Value ??= new Stack<IDictionary<string, object?>>();
            var dict = ExtractKvps(state);
            _scopes.Value.Push(dict);
            return new Disposer(() => _scopes.Value!.Pop());
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var stateProps = ExtractKvps(state);
            var scopeProps = new Dictionary<string, object?>();
            if (_scopes.Value is { } stack)
            {
                // Iterate scopes top-to-bottom; outer scopes win on duplicate keys
                // (matches Microsoft.Extensions.Logging documented behaviour).
                foreach (var scope in stack.Reverse())
                {
                    foreach (var kvp in scope)
                    {
                        scopeProps[kvp.Key] = kvp.Value;
                    }
                }
            }
            _sink.Add(new CapturedEntry(logLevel, eventId, formatter(state, exception), stateProps, scopeProps));
        }

        private static Dictionary<string, object?> ExtractKvps<TState>(TState state)
        {
            var dict = new Dictionary<string, object?>();
            if (state is IEnumerable<KeyValuePair<string, object?>> kvps)
            {
                foreach (var kvp in kvps)
                {
                    // {OriginalFormat} is the raw message template — useful info
                    // but not a structured property; skip so assertions stay clean.
                    if (kvp.Key == "{OriginalFormat}") continue;
                    dict[kvp.Key] = kvp.Value;
                }
            }
            return dict;
        }

        private sealed class Disposer : IDisposable
        {
            private readonly Action _onDispose;
            public Disposer(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }
}
