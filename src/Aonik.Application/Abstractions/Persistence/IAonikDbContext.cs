using Aonik.Domain.Autonumbering.Entities;
using Aonik.Platform.Entities.Cms;
using Aonik.Finance.Entities.Catalog;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Entities.Features;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Entities.Operations;
using Aonik.Domain.PersonalFinance.Entities;
using Aonik.Platform.Entities.ReferenceData;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Entities.Party;
using Microsoft.EntityFrameworkCore;
using PartyEntity = Aonik.Platform.Entities.Party.Party;

namespace Aonik.Application.Abstractions.Persistence;

public interface IAonikDbContext
{
    // Identity
    DbSet<Tenant> Tenants { get; set; }
    DbSet<TenantCountry> TenantCountries { get; set; }
    DbSet<TenantCurrency> TenantCurrencies { get; set; }
    DbSet<User> Users { get; set; }
    DbSet<Role> Roles { get; set; }
    DbSet<Permission> Permissions { get; set; }
    DbSet<UserRole> UserRoles { get; set; }
    DbSet<RolePermission> RolePermissions { get; set; }
    DbSet<UserParty> UserParties { get; set; }
    DbSet<VerificationChallenge> VerificationChallenges { get; set; }

    // Autonumbering
    DbSet<AutonumberProfile> AutonumberProfiles { get; set; }
    DbSet<AutonumberReservation> AutonumberReservations { get; set; }

    // Settings
    DbSet<Setting> Settings { get; set; }

    // Reference Data
    DbSet<ReferenceDataItem> ReferenceDataItems { get; set; }
    DbSet<Country> Countries { get; set; }
    DbSet<Currency> Currencies { get; set; }
    DbSet<CountryCurrency> CountryCurrencies { get; set; }

    // Party
    DbSet<PartyEntity> Parties { get; set; }
    DbSet<PartyAddress> PartyAddresses { get; set; }
    DbSet<PartyContact> PartyContacts { get; set; }
    DbSet<PartyConsent> PartyConsents { get; set; }
    DbSet<PersonProfile> PersonProfiles { get; set; }
    DbSet<BusinessProfile> BusinessProfiles { get; set; }
    DbSet<ExternalAccount> ExternalAccounts { get; set; }
    DbSet<PartyRoleAssignment> PartyRoleAssignments { get; set; }
    DbSet<PartyRelationship> PartyRelationships { get; set; }

    // CMS
    DbSet<ContentBlock> ContentBlocks { get; set; }
    DbSet<ContentBlockMedia> ContentBlockMedia { get; set; }

    // Catalog
    DbSet<CatalogBillerCategory> CatalogBillerCategories { get; set; }
    DbSet<CatalogBiller> CatalogBillers { get; set; }
    DbSet<CatalogBillerService> CatalogBillerServices { get; set; }

    // Compliance
    DbSet<ScreeningCheck> ScreeningChecks { get; set; }
    DbSet<ComplianceCase> ComplianceCases { get; set; }
    DbSet<AuditLog> AuditLogs { get; set; }
    DbSet<Document> Documents { get; set; }
    DbSet<DocumentFile> DocumentFiles { get; set; }
    DbSet<DocumentUsage> DocumentUsages { get; set; }
    DbSet<DocumentVerification> DocumentVerifications { get; set; }
    DbSet<DocumentVersion> DocumentVersions { get; set; }

    // Features
    DbSet<TenantFeature> TenantFeatures { get; set; }

    // Operations
    DbSet<WorkItem> WorkItems { get; set; }
    DbSet<Job> Jobs { get; set; }

    // Notifications
    DbSet<Notification> Notifications { get; set; }
    DbSet<NotificationTemplate> NotificationTemplates { get; set; }
    DbSet<NotificationTemplateBinding> NotificationTemplateBindings { get; set; }
    DbSet<WebhookSubscription> WebhookSubscriptions { get; set; }

    // Personal Finance
    DbSet<PersonalProfile> PersonalProfiles { get; set; }
    DbSet<Household> Households { get; set; }
    DbSet<HouseholdMember> HouseholdMembers { get; set; }
    DbSet<PersonalAccount> PersonalAccounts { get; set; }
    DbSet<PersonalTransaction> PersonalTransactions { get; set; }
    DbSet<CategorisationRule> CategorisationRules { get; set; }
    DbSet<BudgetLine> BudgetLines { get; set; }
    DbSet<Bill> Bills { get; set; }
    DbSet<Subscription> Subscriptions { get; set; }
    DbSet<Goal> Goals { get; set; }
    DbSet<Budget> Budgets { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
