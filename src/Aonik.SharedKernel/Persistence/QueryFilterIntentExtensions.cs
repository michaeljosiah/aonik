using Microsoft.EntityFrameworkCore;

namespace Aonik.SharedKernel.Persistence;

/// <summary>
/// Intent-revealing replacements for <see cref="EntityFrameworkQueryableExtensions.IgnoreQueryFilters{TEntity}(IQueryable{TEntity})"/>.
/// Bare <c>IgnoreQueryFilters</c> is banned via the project-level
/// <c>BannedSymbols.txt</c>; callers must opt in to one of these
/// narrower methods so a code reviewer can tell at a glance which filter
/// is being relaxed and why.
/// </summary>
/// <remarks>
/// <para>
/// Both methods compile to the same EF Core call — IgnoreQueryFilters
/// has no per-filter targeting at the framework level. The value is
/// purely in the explicit opt-in: a future grep for "AcrossTenants"
/// finds every cross-tenant query, and a grep for "IncludeSoftDeleted"
/// finds every soft-delete-bypass. Neither query was greppable when
/// the codebase used bare IgnoreQueryFilters.
/// </para>
/// <para>
/// When a query needs BOTH bypasses (rare — e.g. demo-seed cleanup that
/// re-soft-deletes wrong-tenant rows), call them in sequence:
/// <c>.IncludeSoftDeleted().AcrossTenants()</c>. EF treats the second
/// call as a no-op once filters are already disabled, so the cost is
/// only the second method-group lookup.
/// </para>
/// </remarks>
public static class QueryFilterIntentExtensions
{
    /// <summary>
    /// Bypasses ALL EF query filters on the queried entity, declaring
    /// the caller's intent as "I need to see soft-deleted rows".
    /// Typical use: uniqueness-check-before-INSERT (where a previously
    /// soft-deleted row still occupies the index slot) and admin "show
    /// deleted" lists.
    /// </summary>
    /// <remarks>
    /// The caller MUST still apply an explicit <c>TenantId</c> filter
    /// in the WHERE clause; this method does not preserve tenant
    /// scoping by itself (EF's <c>IgnoreQueryFilters</c> disables every
    /// filter at once). The cross-tenant tests at
    /// <c>tests/Aonik.Application.Tests/Persistence/IgnoreQueryFiltersCrossTenantTests.cs</c>
    /// document the safe usage patterns for both this method and
    /// <see cref="AcrossTenants{TEntity}"/>.
    /// </remarks>
    public static IQueryable<TEntity> IncludeSoftDeleted<TEntity>(this IQueryable<TEntity> source)
        where TEntity : class =>
#pragma warning disable RS0030 // Banned API — this file is the intent-revealing wrapper.
        source.IgnoreQueryFilters();
#pragma warning restore RS0030

    /// <summary>
    /// Bypasses ALL EF query filters on the queried entity, declaring
    /// the caller's intent as "I need to see rows from every tenant".
    /// Typical use: cron jobs that fan out per-tenant work, platform-
    /// admin lookups, and platform-level rows where TenantId is
    /// <see cref="Guid.Empty"/> by design.
    /// </summary>
    /// <remarks>
    /// Cross-tenant queries are a real cross-cutting concern — they
    /// should never be added without explicit thought about the
    /// tenant-isolation implications. Reviewers should treat the
    /// presence of this method call as a load-bearing comment that
    /// says "we deliberately span tenants here".
    /// </remarks>
    public static IQueryable<TEntity> AcrossTenants<TEntity>(this IQueryable<TEntity> source)
        where TEntity : class =>
#pragma warning disable RS0030 // Banned API — this file is the intent-revealing wrapper.
        source.IgnoreQueryFilters();
#pragma warning restore RS0030
}
