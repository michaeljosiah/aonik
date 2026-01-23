using Aonik.Application.Abstractions.Persistence;
using Aonik.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds global permissions for the AONIK platform.
/// Permissions are global (not tenant-scoped) and define what actions can be performed.
/// Roles are tenant-specific and are created manually per tenant.
/// Users are provisioned via JIT (Just-In-Time) authentication.
/// </summary>
public class IdentitySeedService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ILogger<IdentitySeedService> _logger;

    public IdentitySeedService(IAonikDbContext dbContext, ILogger<IdentitySeedService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Seeds global permissions. Idempotent - safe to call multiple times.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting identity seed process...");

        await SeedPermissionsAsync(cancellationToken);

        _logger.LogInformation("Identity seed process completed successfully");
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Seeding permissions...");

        var permissions = new[]
        {
            // Invoice permissions
            new Permission { Key = "Invoice.Create", Description = "Create new invoices" },
            new Permission { Key = "Invoice.Read", Description = "View invoices" },
            new Permission { Key = "Invoice.Update", Description = "Update existing invoices" },
            new Permission { Key = "Invoice.Delete", Description = "Delete invoices" },
            new Permission { Key = "Invoice.Issue", Description = "Issue draft invoices" },

            // Payment permissions
            new Permission { Key = "Payment.Create", Description = "Create payment intents" },
            new Permission { Key = "Payment.Read", Description = "View payments" },
            new Permission { Key = "Payment.Capture", Description = "Capture authorized payments" },
            new Permission { Key = "Payment.Cancel", Description = "Cancel payments" },
            new Permission { Key = "Payment.Refund", Description = "Refund payments" },

            // Ledger permissions
            new Permission { Key = "Ledger.Read", Description = "View ledger accounts and entries" },
            new Permission { Key = "Ledger.Write", Description = "Create/modify ledger accounts and journal entries" },
            new Permission { Key = "Ledger.Reconcile", Description = "Reconcile ledger accounts" },

            // Tenant admin permissions (platform-wide)
            new Permission { Key = "Tenants.Read", Description = "View tenants" },
            new Permission { Key = "Tenants.Write", Description = "Create and manage tenants" },

            // Settings permissions (tenant-scoped operations)
            new Permission { Key = "Settings.Read", Description = "View tenant settings" },
            new Permission { Key = "Settings.Write", Description = "Modify tenant settings" },

            // User management permissions (tenant-scoped)
            new Permission { Key = "Users.Read", Description = "View users in tenant" },
            new Permission { Key = "Users.Invite", Description = "Invite users to tenant" },
            new Permission { Key = "Users.Manage", Description = "Manage user roles and permissions" },
            new Permission { Key = "Users.Deactivate", Description = "Deactivate users" },

            // UserInfo permissions (for user profile endpoints)
            new Permission { Key = "UserInfo.Read", Description = "View user information and profile" },
            new Permission { Key = "UserInfo.Update", Description = "Update user information and profile" },

            // Role management permissions (tenant-scoped)
            new Permission { Key = "Roles.Read", Description = "View roles in tenant" },
            new Permission { Key = "Roles.Create", Description = "Create roles in tenant" },
            new Permission { Key = "Roles.Update", Description = "Update roles in tenant" },
            new Permission { Key = "Roles.Delete", Description = "Delete roles in tenant" },

            // Catalog permissions
            new Permission { Key = "Catalog.Read", Description = "View catalog and biller data" }
        };

        var existingKeys = await _dbContext.Permissions
            .Select(p => p.Key)
            .ToListAsync(cancellationToken);

        var existingKeySet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var newPermissions = permissions
            .Where(p => !existingKeySet.Contains(p.Key))
            .ToList();

        if (newPermissions.Any())
        {
            await _dbContext.Permissions.AddRangeAsync(newPermissions, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Seeded {Count} new permissions", newPermissions.Count);
        }
        else
        {
            _logger.LogInformation("All permissions already exist - skipping seed");
        }
    }
}
