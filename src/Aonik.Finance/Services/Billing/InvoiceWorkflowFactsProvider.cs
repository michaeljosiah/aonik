using System.Globalization;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Finance.Services.Billing;

internal sealed class InvoiceWorkflowFactsProvider(
    FinanceDbContext database, ITenantProvider tenantProvider, IPermissionService permissions, IUserDirectoryReader users)
    : IWorkflowFactsProvider
{
    public string SubjectType => "Invoice";
    public string Schema => "invoice-approval.v1";

    public async Task<WorkflowFactsSnapshot?> LoadAsync(Guid subjectId, WorkflowActor actor, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        if (actor.Type != "User" || !(await users.GetByIdsAsync(tenantId, [actor.Id], cancellationToken))
            .Any(user => user.UserId == actor.Id && user.Status == "Active")
            || !await permissions.HasPermissionAsync(actor.Id, "Invoice.Read", cancellationToken))
            throw new PermissionDeniedException("Invoice.Read");
        Invoice? invoice = await database.Invoices.AsNoTracking().SingleOrDefaultAsync(
            invoice => invoice.TenantId == tenantId && invoice.Id == subjectId, cancellationToken);
        if (invoice is null) return null;
        return FromInvoice(invoice);
    }

    internal static WorkflowFactsSnapshot FromInvoice(Invoice invoice)
    {
        var facts = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["invoice.id"] = JsonSerializer.SerializeToElement(invoice.Id.ToString("D")),
            ["invoice.total"] = JsonSerializer.SerializeToElement(invoice.Total.ToString("0.############################", CultureInfo.InvariantCulture)),
            ["invoice.currency"] = JsonSerializer.SerializeToElement(invoice.Currency.ToUpperInvariant()),
            ["invoice.status"] = JsonSerializer.SerializeToElement(invoice.Status),
            ["invoice.customerId"] = JsonSerializer.SerializeToElement(invoice.CustomerAccountId.ToString("D")),
        };
        string? blocked = invoice.Status != "Draft" ? "InvoiceNotDraft"
            : invoice.Total < 0 || invoice.CustomerAccountId == Guid.Empty || invoice.Currency.Length != 3
                || invoice.Currency.Any(character => !char.IsAsciiLetter(character)) ? "InvoiceIncomplete" : null;
        // No authoritative duplicate/risk provider exists here: absence must not become a clean result.
        return new("invoice-approval.v1", facts, Convert.ToBase64String(invoice.RowVersion), blocked, invoice.CreatedBy);
    }
}
