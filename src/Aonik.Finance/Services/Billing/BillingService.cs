using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Finance.Persistence;
using Aonik.Finance.Contracts.Models.Billing;
using Aonik.Finance.Contracts.Services.Billing;

using Aonik.SharedKernel.Abstractions;
using Aonik.Finance.Entities.Billing;

namespace Aonik.Finance.Services.Billing;

internal class BillingService : FinanceServiceBase, IBillingService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly Services.Observability.FinanceMetrics _metrics;

    public BillingService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider,
        Services.Observability.FinanceMetrics metrics)
        : base(currentUserProvider, permissionService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _metrics = metrics;
    }

    public async Task<InvoiceResponse> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Create", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerAccountId = request.CustomerId,
            IssueDate = DateTime.UtcNow,
            DueDate = request.DueUtc,
            Currency = request.Currency,
            Status = "Draft",
            ProvenanceJson = "{}",
            Subtotal = 0,
            TaxTotal = 0,
            DiscountTotal = 0,
            Total = 0,
            Lines = new List<InvoiceLine>()
        };

        foreach (var lineItemRequest in request.LineItems)
        {
            var lineTotal = lineItemRequest.Quantity * lineItemRequest.UnitPrice;

            var lineItem = new InvoiceLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                InvoiceId = invoice.Id,
                Description = lineItemRequest.Description,
                Quantity = lineItemRequest.Quantity,
                UnitPrice = lineItemRequest.UnitPrice,
                TaxRate = 0,
                LineTotal = lineTotal,
                MetadataJson = "{}"
            };

            invoice.Lines.Add(lineItem);
        }

        RecalculateInvoiceTotals(invoice);

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Per-tenant invoice creation counter. Tagged with currency so a
        // multi-currency tenant doesn't collapse onto one series.
        _metrics.RecordInvoiceCreated(tenantId, invoice.Currency);

        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId, cancellationToken);

        return invoice == null ? null : MapToResponse(invoice);
    }

    public async Task AddLineToInvoiceAsync(Guid invoiceId, CreateInvoiceLineItemRequest lineRequest, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");

        var lineTotal = lineRequest.Quantity * lineRequest.UnitPrice;

        var lineItem = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            Description = lineRequest.Description,
            Quantity = lineRequest.Quantity,
            UnitPrice = lineRequest.UnitPrice,
            TaxRate = 0,
            LineTotal = lineTotal,
            MetadataJson = "{}"
        };

        invoice.Lines.Add(lineItem);
        RecalculateInvoiceTotals(invoice);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyDiscountAsync(Guid invoiceId, decimal discountTotal, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");

        invoice.DiscountTotal = discountTotal;
        RecalculateInvoiceTotals(invoice);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task IssueInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Issue", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var invoice = await _dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");

        if (invoice.Status != "Draft")
            throw new InvalidOperationException("Only draft invoices can be issued");

        invoice.Status = "Issued";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkInvoiceAsPaidAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var invoice = await _dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");

        if (invoice.Status != "Issued")
            throw new InvalidOperationException("Only issued invoices can be marked as paid");

        invoice.Status = "Paid";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var invoice = await _dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found");

        if (invoice.Status == "Paid")
            throw new InvalidOperationException("Paid invoices cannot be cancelled");

        invoice.Status = "Cancelled";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLineQuantityAsync(Guid invoiceLineId, decimal quantity, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var line = await _dbContext.InvoiceLines
            .FirstOrDefaultAsync(l => l.Id == invoiceLineId && l.TenantId == tenantId, cancellationToken);

        if (line == null)
            throw new InvalidOperationException($"Invoice line {invoiceLineId} not found");

        line.Quantity = quantity;
        line.LineTotal = quantity * line.UnitPrice;

        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == line.InvoiceId, cancellationToken);

        if (invoice != null)
        {
            RecalculateInvoiceTotals(invoice);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLineUnitPriceAsync(Guid invoiceLineId, decimal unitPrice, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Update", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var line = await _dbContext.InvoiceLines
            .FirstOrDefaultAsync(l => l.Id == invoiceLineId && l.TenantId == tenantId, cancellationToken);

        if (line == null)
            throw new InvalidOperationException($"Invoice line {invoiceLineId} not found");

        line.UnitPrice = unitPrice;
        line.LineTotal = line.Quantity * unitPrice;

        var invoice = await _dbContext.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == line.InvoiceId, cancellationToken);

        if (invoice != null)
        {
            RecalculateInvoiceTotals(invoice);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void RecalculateInvoiceTotals(Invoice invoice)
    {
        invoice.Subtotal = invoice.Lines.Sum(x => x.LineTotal);
        invoice.TaxTotal = invoice.Lines.Sum(x => x.LineTotal * x.TaxRate);
        invoice.Total = invoice.Subtotal + invoice.TaxTotal - invoice.DiscountTotal;
    }

    public async Task<IReadOnlyList<InvoiceResponse>> ListInvoicesAsync(string? statusFilter = null, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Invoice.Read", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _dbContext.Invoices
            .Include(i => i.Lines)
            .Where(i => i.TenantId == tenantId);

        if (!string.IsNullOrEmpty(statusFilter))
            query = query.Where(i => i.Status == statusFilter);

        var invoices = await query
            .OrderByDescending(i => i.IssueDate)
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
        {
            return new List<InvoiceResponse>();
        }

        // Two-hop lookup: Invoice → CustomerAccount → Party. We post-fetch
        // both sides in batched queries rather than navigation Includes
        // because the relations are FK-only (no navigation properties on
        // the entities). This mirrors OrderService.ListOrdersAsync.
        var customerAccountIds = invoices
            .Select(i => i.CustomerAccountId)
            .Distinct()
            .ToList();

        var customerAccountToParty = await _dbContext.CustomerAccounts
            .AsNoTracking()
            .Where(ca => ca.TenantId == tenantId && customerAccountIds.Contains(ca.Id))
            .Select(ca => new { ca.Id, ca.CustomerPartyId })
            .ToDictionaryAsync(x => x.Id, x => x.CustomerPartyId, cancellationToken);

        var partyIds = customerAccountToParty.Values.Distinct().ToList();
        var partyNamesById = partyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Parties
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && partyIds.Contains(p.Id))
                .Select(p => new { p.Id, p.DisplayName })
                .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken);

        return invoices.Select(invoice =>
        {
            Guid? partyId = null;
            string partyName = string.Empty;
            if (customerAccountToParty.TryGetValue(invoice.CustomerAccountId, out var resolvedPartyId))
            {
                partyId = resolvedPartyId;
                if (partyNamesById.TryGetValue(resolvedPartyId, out var name))
                {
                    partyName = name;
                }
            }
            return MapToResponse(invoice, partyId, partyName);
        }).ToList();
    }

    private static InvoiceResponse MapToResponse(Invoice invoice)
    {
        return MapToResponse(invoice, customerPartyId: null, customerName: string.Empty);
    }

    private static InvoiceResponse MapToResponse(
        Invoice invoice,
        Guid? customerPartyId,
        string customerName)
    {
        return new InvoiceResponse(
            invoice.Id,
            invoice.CustomerAccountId,
            invoice.Id.ToString("N"),
            invoice.Currency,
            invoice.Total,
            Enum.Parse<InvoiceStatus>(invoice.Status),
            invoice.IssueDate,
            invoice.DueDate,
            invoice.Lines.Select(li => new InvoiceLineItemResponse(
                li.Id,
                li.Description,
                li.Quantity,
                li.UnitPrice,
                li.LineTotal)).ToList(),
            customerPartyId,
            customerName);
    }

}
