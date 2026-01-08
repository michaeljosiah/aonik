using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Orders.Entities;

public class Order : AuditableEntity
{
    public Guid OrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public string OrderType { get; private set; } = string.Empty;
    public decimal AmountIn { get; private set; }
    public string CurrencyIn { get; private set; } = string.Empty;
    public decimal? AmountOut { get; private set; }
    public string? CurrencyOut { get; private set; }
    public string FeesJson { get; private set; } = string.Empty;
    public Guid? FxQuoteId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string ProvenanceJson { get; private set; } = string.Empty;

    private readonly List<OrderPartyRole> _partyRoles = new();
    public IReadOnlyCollection<OrderPartyRole> PartyRoles => _partyRoles.AsReadOnly();

    private readonly List<OrderHistoryEvent> _historyEvents = new();
    public IReadOnlyCollection<OrderHistoryEvent> HistoryEvents => _historyEvents.AsReadOnly();

    private Order() { }

    public Order(Guid tenantId, string orderType, decimal amountIn, string currencyIn, decimal? amountOut = null, string? currencyOut = null)
    {
        OrderId = Id;
        TenantId = tenantId;
        OrderType = orderType;
        AmountIn = amountIn;
        CurrencyIn = currencyIn;
        AmountOut = amountOut;
        CurrencyOut = currencyOut;
        FeesJson = "{}";
        Status = "Draft";
        ProvenanceJson = "{}";
    }

    public void UpdateFees(string feesJson)
    {
        FeesJson = feesJson;
    }

    public void AttachFxQuote(Guid fxQuoteId)
    {
        FxQuoteId = fxQuoteId;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void AddPartyRole(OrderPartyRole partyRole)
    {
        _partyRoles.Add(partyRole);
    }

    public void AddHistoryEvent(OrderHistoryEvent historyEvent)
    {
        _historyEvents.Add(historyEvent);
    }

    public void Submit()
    {
        if (Status != "Draft")
            throw new InvalidOperationException("Only draft orders can be submitted");

        Status = "Submitted";
    }

    public void Complete()
    {
        Status = "Completed";
    }

    public void Cancel()
    {
        if (Status == "Completed")
            throw new InvalidOperationException("Completed orders cannot be cancelled");

        Status = "Cancelled";
    }
}
