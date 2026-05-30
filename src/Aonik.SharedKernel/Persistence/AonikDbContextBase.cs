using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;

namespace Aonik.SharedKernel.Persistence;

/// <summary>
/// Base class for all module-scoped DbContexts in AONIK.
/// Provides shared multi-tenancy enforcement (query filters + write guards),
/// audit field stamping, and soft-delete conversion.
/// 
/// Derived contexts supply their own DbSets, OnModelCreating configurations,
/// and optional domain-specific SaveChanges hooks via <see cref="OnBeforeSave"/>.
/// </summary>
public abstract class AonikDbContextBase : DbContext
{
    private readonly ITenantProvider? _tenantProvider;
    private readonly ICurrentUserProvider? _currentUserProvider;
    private readonly IClock? _clock;

    /// <summary>
    /// Resolves the current tenant from the provider, or null when no tenant context
    /// is available (design-time, migrations, background jobs without tenant scope).
    /// </summary>
    protected Guid? CurrentTenantId =>
        _tenantProvider?.TryGetCurrentTenantId(out var tenantId) == true ? tenantId : null;

    /// <summary>
    /// Provides access to the tenant provider for derived contexts that need it.
    /// </summary>
    protected ITenantProvider? TenantProvider => _tenantProvider;

    /// <summary>
    /// Provides access to the current user provider for derived contexts that need it.
    /// </summary>
    protected ICurrentUserProvider? CurrentUserProvider => _currentUserProvider;

    /// <summary>
    /// Provides access to the clock for derived contexts that need it.
    /// </summary>
    protected IClock? Clock => _clock;

    protected AonikDbContextBase(
        DbContextOptions options,
        ITenantProvider? tenantProvider = null,
        ICurrentUserProvider? currentUserProvider = null,
        IClock? clock = null)
        : base(options)
    {
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _clock = clock;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceTenantOnWrites();
        OnBeforeSave();
        UpdateAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Maps the transactional outbox / inbox tables onto every derived context so
    /// that <see cref="EnqueueIntegrationEvent"/> can stage events through whichever
    /// module context performed the write. Derived contexts must call
    /// <c>base.OnModelCreating(modelBuilder)</c> (they already do) for this to apply.
    /// The canonical migration stream lives in <c>AonikDbContext</c>, which inherits
    /// this mapping and is the only context that owns the schema.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
    }

    /// <summary>
    /// Stages an integration event into the transactional outbox. The row is added to
    /// this context's change tracker, so it commits atomically with the surrounding
    /// <see cref="SaveChangesAsync"/> — closing the crash window between persisting a
    /// domain change and publishing its event. Drained asynchronously by the outbox
    /// processor in the Worker. Call this BEFORE <see cref="SaveChangesAsync"/>, never after.
    /// </summary>
    public void EnqueueIntegrationEvent(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = integrationEvent.GetType();
        var now = _clock?.UtcNow ?? DateTime.UtcNow;

        Set<OutboxMessage>().Add(new OutboxMessage
        {
            EventId = integrationEvent.EventId,
            EventType = eventType.FullName!,
            Payload = JsonSerializer.Serialize(integrationEvent, eventType, OutboxSerialization.Options),
            TenantId = CurrentTenantId,
            OccurredAt = integrationEvent.OccurredAt,
            CreatedAt = now,
            Attempts = 0,
            TraceParent = Activity.Current?.Id,
        });
    }

    /// <summary>
    /// Override in derived contexts to add domain-specific pre-save logic
    /// (e.g. populating compatibility columns, generating numbers).
    /// Called after tenant enforcement but before audit field stamping.
    /// </summary>
    protected virtual void OnBeforeSave()
    {
    }

    /// <summary>
    /// Configures <see cref="AuditableEntity.RowVersion"/> as an optimistic concurrency token
    /// on every entity inheriting from <see cref="AuditableEntity"/>.
    /// On SQL Server the column is mapped to the native <c>rowversion</c> type (8-byte,
    /// auto-incremented by the database engine on every INSERT/UPDATE).
    /// Call this from <see cref="DbContext.OnModelCreating"/> in every derived context.
    /// </summary>
    protected static void ConfigureRowVersions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(AuditableEntity.RowVersion))
                    .IsRowVersion()
                    .ValueGeneratedOnAddOrUpdate()
                    .HasAnnotation("Relational:ColumnType", "rowversion");
            }
        }
    }

    /// <summary>
    /// Applies <see cref="ITenantScoped"/> query filters to all tenant-scoped entities
    /// registered in the model. Call this from <see cref="DbContext.OnModelCreating"/>.
    /// 
    /// Filter logic: no tenant context → show all rows; otherwise show rows matching
    /// the current tenant OR rows with TenantId == Guid.Empty (global/system rows).
    /// 
    /// For entities that also inherit from <see cref="AuditableEntity"/>, the filter
    /// additionally excludes soft-deleted rows (IsDeleted == true).
    /// </summary>
    protected void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        if (_tenantProvider == null)
            return;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(ITenantScoped).IsAssignableFrom(clrType))
            {
                var parameter = Expression.Parameter(clrType, "e");
                var property = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                var currentTenantId = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
                var noTenantContext = Expression.Equal(
                    currentTenantId,
                    Expression.Constant(null, typeof(Guid?)));
                var tenantIdAsNullable = Expression.Convert(property, typeof(Guid?));
                var equalsTenant = Expression.Equal(tenantIdAsNullable, currentTenantId);
                var equalsGlobal = Expression.Equal(property, Expression.Constant(Guid.Empty));
                Expression filter = Expression.OrElse(noTenantContext, Expression.OrElse(equalsTenant, equalsGlobal));

                // Combine with soft-delete filter for AuditableEntity types
                if (typeof(AuditableEntity).IsAssignableFrom(clrType))
                {
                    var isDeleted = Expression.Property(parameter, nameof(AuditableEntity.IsDeleted));
                    var notDeleted = Expression.Not(isDeleted);
                    filter = Expression.AndAlso(filter, notDeleted);
                }

                var lambda = Expression.Lambda(filter, parameter);
                modelBuilder.Entity(clrType).HasQueryFilter(lambda);
            }
        }
    }

    /// <summary>
    /// Applies a nullable-TenantId query filter for entities where TenantId is Guid? (nullable).
    /// These entities can exist without a tenant (TenantId == null means global/platform-level).
    /// Call this from <see cref="DbContext.OnModelCreating"/> after <see cref="ApplyTenantQueryFilters"/>.
    /// 
    /// For entities that also inherit from <see cref="AuditableEntity"/>, the filter
    /// additionally excludes soft-deleted rows (IsDeleted == true).
    /// </summary>
    protected void ApplyNullableTenantQueryFilter(ModelBuilder modelBuilder, Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        var property = Expression.Property(parameter, "TenantId");
        var currentTenantId = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        var noTenantContext = Expression.Equal(
            currentTenantId,
            Expression.Constant(null, typeof(Guid?)));
        var tenantIdAsNullable = Expression.Convert(property, typeof(Guid?));
        var equalsTenant = Expression.Equal(tenantIdAsNullable, currentTenantId);
        var equalsNull = Expression.Equal(property, Expression.Constant(null, property.Type));
        Expression filter = Expression.OrElse(noTenantContext, Expression.OrElse(equalsTenant, equalsNull));

        // Combine with soft-delete filter for AuditableEntity types
        if (typeof(AuditableEntity).IsAssignableFrom(clrType))
        {
            var isDeleted = Expression.Property(parameter, nameof(AuditableEntity.IsDeleted));
            var notDeleted = Expression.Not(isDeleted);
            filter = Expression.AndAlso(filter, notDeleted);
        }

        var lambda = Expression.Lambda(filter, parameter);
        modelBuilder.Entity(clrType).HasQueryFilter(lambda);
    }

    /// <summary>
    /// Applies a soft-delete query filter (IsDeleted == false) to all <see cref="AuditableEntity"/>
    /// types that do NOT implement <see cref="ITenantScoped"/> and do NOT already have a query
    /// filter from <see cref="ApplyNullableTenantQueryFilter"/>. Call this from
    /// <see cref="DbContext.OnModelCreating"/> after tenant filters are applied.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="excludeTypes">
    /// Types that already have a query filter applied (e.g. via
    /// <see cref="ApplyNullableTenantQueryFilter"/>). These will be skipped.
    /// </param>
    protected static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder, params Type[] excludeTypes)
    {
        var excludeSet = new HashSet<Type>(excludeTypes);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // Skip tenant-scoped entities (handled by ApplyTenantQueryFilters)
            // and explicitly excluded types (handled by ApplyNullableTenantQueryFilter)
            if (typeof(ITenantScoped).IsAssignableFrom(clrType))
                continue;
            if (excludeSet.Contains(clrType))
                continue;
            if (!typeof(AuditableEntity).IsAssignableFrom(clrType))
                continue;

            var parameter = Expression.Parameter(clrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(AuditableEntity.IsDeleted));
            var notDeleted = Expression.Not(isDeleted);
            var lambda = Expression.Lambda(notDeleted, parameter);

            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }
    }

    /// <summary>
    /// Enforces tenant isolation on all pending writes (Add/Modify/Delete) for
    /// entities implementing <see cref="ITenantScoped"/>.
    /// - On Add: stamps CurrentTenantId when TenantId is Guid.Empty
    /// - On Modify/Delete: throws if entity belongs to a different tenant
    /// - Role entities with TenantId == Guid.Empty are exempt (system/global roles)
    /// </summary>
    private void EnforceTenantOnWrites()
    {
        var tenantEntries = ChangeTracker.Entries<ITenantScoped>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (tenantEntries.Count == 0)
            return;

        if (_tenantProvider == null || !_tenantProvider.TryGetCurrentTenantId(out var currentTenantId))
            throw new InvalidOperationException("Tenant context is required for tenant-scoped writes.");

        foreach (var entry in tenantEntries)
        {
            var tenantIdProperty = entry.Property(nameof(ITenantScoped.TenantId));
            var tenantId = (Guid)tenantIdProperty.CurrentValue!;

            if (entry.State == EntityState.Added && tenantId == Guid.Empty)
            {
                // Skip stamping for global/system entities (e.g. Role with empty TenantId)
                if (IsGlobalEntity(entry.Entity))
                    continue;

                tenantIdProperty.CurrentValue = currentTenantId;
                continue;
            }

            if (entry.State is EntityState.Modified or EntityState.Deleted && tenantId != currentTenantId)
            {
                // Allow modification of global/system entities
                if (IsGlobalEntity(entry.Entity) && tenantId == Guid.Empty)
                    continue;

                throw new InvalidOperationException(
                    $"Tenant mismatch detected for {entry.Metadata.ClrType.Name} ({tenantId}).");
            }
        }
    }

    /// <summary>
    /// Override to specify entity types that can exist with TenantId == Guid.Empty
    /// and should be exempt from tenant stamping/mismatch checks.
    /// Default implementation checks for Aonik.Domain.Identity.Entities.Role by type name
    /// to preserve backward compatibility.
    /// </summary>
    protected virtual bool IsGlobalEntity(object entity)
    {
        // Check by type name to avoid coupling the base class to Domain.Identity
        return entity.GetType().Name == "Role";
    }

    /// <summary>
    /// Stamps CreatedAt/By, UpdatedAt/By audit fields and converts hard deletes
    /// to soft deletes (IsDeleted + DeletedAt/By) for all <see cref="AuditableEntity"/> instances.
    /// </summary>
    private void UpdateAuditFields()
    {
        var userId = _currentUserProvider?.GetCurrentUserId();
        var now = _clock?.UtcNow ?? DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.DeletedBy = userId;
                    break;
            }
        }
    }
}
