using System.Security.Cryptography;

using Aonik.Commerce.Entities.Cart;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aonik.Api.Tests;

/// <summary>
/// Supplies only the initial InMemory cart token because TestHost drops empty HTTP headers.
/// Native rowversion changes and concurrency remain covered by the SQL tests.
/// </summary>
internal sealed class CartVersionTestInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AssignInitialVersions(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AssignInitialVersions(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void AssignInitialVersions(DbContext? context)
    {
        if (context is null) return;
        foreach (var entry in context.ChangeTracker.Entries<Cart>()
                     .Where(entry => entry.State == EntityState.Added && entry.Entity.RowVersion.Length == 0))
            entry.Entity.RowVersion = RandomNumberGenerator.GetBytes(8);
    }
}
