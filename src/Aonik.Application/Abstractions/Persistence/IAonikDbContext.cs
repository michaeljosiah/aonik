using Aonik.Domain.Ai.Entities;
using Aonik.Domain.Billing.Entities;
using Aonik.Domain.Ledger.Entities;
using Aonik.Domain.Payments.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Abstractions.Persistence;

public interface IAonikDbContext
{
    DbSet<LedgerAccount> LedgerAccounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLineItem> InvoiceLineItems { get; }
    DbSet<PaymentIntent> PaymentIntents { get; }
    DbSet<Insight> Insights { get; }
    DbSet<Signal> Signals { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
