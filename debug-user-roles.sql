-- ============================================================================
-- AONIK Bootstrap Reset & User Debug SQL
-- ============================================================================
-- This script provides:
--   A) Commands to RESET database for fresh bootstrap
--   B) Queries to DEBUG user roles and permissions
-- 
-- VERIFIED against actual Entity definitions and EF Core configurations
-- ============================================================================

-- ============================================================================
-- SECTION A: RESET DATABASE FOR FRESH BOOTSTRAP
-- ============================================================================
-- ⚠️ WARNING: These DELETE statements will remove ALL data!
-- ⚠️ Only run this when you want to start bootstrap from scratch!
-- ⚠️ CANNOT BE UNDONE - backup your database first if needed!
-- ============================================================================

-- UNCOMMENT THE SECTION BELOW TO DELETE ALL DATA:

/*
-- Delete in correct order to respect foreign key constraints

-- Step 1: Delete Identity & User-related data
PRINT 'Deleting UserRoles...';
DELETE FROM UserRoles;

PRINT 'Deleting RolePermissions...';
DELETE FROM RolePermissions;

PRINT 'Deleting UserParties...';
DELETE FROM UserParties;

PRINT 'Deleting VerificationChallenges...';
DELETE FROM VerificationChallenges;

PRINT 'Deleting Users...';
DELETE FROM Users;

PRINT 'Deleting Roles...';
DELETE FROM Roles;

PRINT 'Deleting Permissions...';
DELETE FROM Permissions;

-- Step 2: Delete Settings and Reference Data
PRINT 'Deleting Settings...';
DELETE FROM Settings;

PRINT 'Deleting ReferenceDataItems...';
DELETE FROM ReferenceDataItems;

-- Step 3: Delete Audit & Compliance Data
PRINT 'Deleting AuditLogs...';
DELETE FROM AuditLogs;

PRINT 'Deleting ComplianceCases...';
DELETE FROM ComplianceCases;

PRINT 'Deleting ScreeningChecks...';
DELETE FROM ScreeningChecks;

-- Step 4: Delete AI & Agent Data (optional - comment out if you want to keep)
PRINT 'Deleting AI and Agent data...';
DELETE FROM AiFeedbacks;
DELETE FROM EvalRuns;
DELETE FROM EvalSuites;
DELETE FROM AiTraces;
DELETE FROM AiRuns;
DELETE FROM PromptSpecs;
DELETE FROM ToolSpecs;
DELETE FROM AiPolicies;
DELETE FROM AiRoutePolicies;
DELETE FROM AiModels;
DELETE FROM AiProviders;
DELETE FROM Proposals;
DELETE FROM AgentRuns;
DELETE FROM Agents;
DELETE FROM OrchestratorPolicies;
DELETE FROM Insights;
DELETE FROM Signals;

-- Step 5: Delete Business Data (optional - comment out if you want to keep)
PRINT 'Deleting business data...';
DELETE FROM InvoiceLines;
DELETE FROM InvoiceAllocations;
DELETE FROM Invoices;
DELETE FROM CustomerAccounts;
DELETE FROM DunningPlans;
DELETE FROM OrderNotes;
DELETE FROM OrderHistoryEvents;
DELETE FROM OrderFulfilmentRefs;
DELETE FROM OrderFundingRefs;
DELETE FROM OrderPartyRoles;
DELETE FROM Orders;
DELETE FROM Chargebacks;
DELETE FROM Refunds;
DELETE FROM Payouts;
DELETE FROM Payments;
DELETE FROM PaymentIntents;
DELETE FROM JournalEntryLines;
DELETE FROM JournalEntries;
DELETE FROM BalanceSnapshots;
DELETE FROM LedgerAccounts;
DELETE FROM Ledgers;

-- Step 6: Delete Party Data
PRINT 'Deleting Party data...';
DELETE FROM PartyRoleAssignments;
DELETE FROM ExternalAccounts;
DELETE FROM BusinessProfiles;
DELETE FROM PersonProfiles;
DELETE FROM PartyConsents;
DELETE FROM PartyContacts;
DELETE FROM PartyAddresses;
DELETE FROM Parties;

-- Step 7: Delete Personal Finance Data (optional)
PRINT 'Deleting Personal Finance data...';
DELETE FROM BudgetLines;
DELETE FROM Budgets;
DELETE FROM Goals;
DELETE FROM Subscriptions;
DELETE FROM Bills;
DELETE FROM CategorisationRules;
DELETE FROM PersonalTransactions;
DELETE FROM HouseholdMembers;
DELETE FROM Households;
DELETE FROM PersonalProfiles;

-- Step 8: Delete Catalog Data (optional)
PRINT 'Deleting Catalog data...';
DELETE FROM CatalogBillerServices;
DELETE FROM CatalogBillers;
DELETE FROM CatalogBillerCategories;

-- Step 9: Delete Partner & Pricing Data (optional)
PRINT 'Deleting Partner and Pricing data...';
DELETE FROM Transmissions;
DELETE FROM PayoutSchemas;
DELETE FROM RoutingRules;
DELETE FROM Connectors;
DELETE FROM PartnerBranches;
DELETE FROM Partners;
DELETE FROM FxQuotes;
DELETE FROM FeePolicies;
DELETE FROM LimitsPolicies;

-- Step 10: Delete Operations Data
PRINT 'Deleting Operations data...';
DELETE FROM Jobs;
DELETE FROM WorkItems;

-- Step 11: Delete Notifications
PRINT 'Deleting Notifications...';
DELETE FROM WebhookSubscriptions;
DELETE FROM Notifications;

-- Step 12: FINALLY - Delete Tenants (must be last!)
PRINT 'Deleting Tenants...';
DELETE FROM Tenants;

PRINT '✅ All data deleted successfully! Database is ready for fresh bootstrap.';
PRINT '   Run POST /bootstrap to create a new tenant and user.';
*/

-- ============================================================================
-- SECTION B: DEBUG USER ROLES AND PERMISSIONS
-- ============================================================================
-- Use these queries to check user data, roles, and permissions
-- ============================================================================

-- 1. Find the user by email (might return nothing if email is NULL)
SELECT 
    u.Id AS UserId,
    u.Email,
    u.TenantId,
    u.Status,
    u.ExternalIssuer,
    u.ExternalSubject,
    u.ExternalTenantId,
    u.Phone,
    u.CreatedAt,
    u.UpdatedAt
FROM Users u
WHERE u.Email = 'michael.josiah@mailinator.com';

-- 1b. Find ALL users with their external identities (use this to identify your user)
SELECT 
    u.Id AS UserId,
    u.Email,
    u.TenantId,
    u.Status,
    u.ExternalIssuer,
    u.ExternalSubject,
    u.ExternalTenantId,
    u.CreatedAt,
    u.UpdatedAt
FROM Users u
ORDER BY u.CreatedAt DESC;

-- 1c. Find user by External Subject (from JWT 'sub' or 'oid' claim)
-- REPLACE 'YOUR-SUBJECT-HERE' with actual value from your JWT token
SELECT 
    u.Id AS UserId,
    u.Email,
    u.TenantId,
    u.Status,
    u.ExternalIssuer,
    u.ExternalSubject,
    u.ExternalTenantId,
    u.Phone,
    u.CreatedAt,
    u.UpdatedAt
FROM Users u
WHERE u.ExternalSubject = 'YOUR-SUBJECT-HERE';

-- 2. Get all roles assigned to this user
SELECT 
    u.Id AS UserId,
    u.Email,
    r.Id AS RoleId,
    r.Name AS RoleName,
    r.TenantId AS RoleTenantId,
    ur.CreatedAt AS AssignedAt
FROM Users u
INNER JOIN UserRoles ur ON u.Id = ur.UserId
INNER JOIN Roles r ON ur.RoleId = r.Id
WHERE u.Email = 'michael.josiah@mailinator.com';

-- 3. Get all permissions for the user's roles
SELECT 
    u.Id AS UserId,
    u.Email,
    r.Name AS RoleName,
    p.Id AS PermissionId,
    p.[Key] AS PermissionKey,
    p.Description AS PermissionDescription
FROM Users u
INNER JOIN UserRoles ur ON u.Id = ur.UserId
INNER JOIN Roles r ON ur.RoleId = r.Id
INNER JOIN RolePermissions rp ON r.Id = rp.RoleId
INNER JOIN Permissions p ON rp.PermissionId = p.Id
WHERE u.Email = 'michael.josiah@mailinator.com'
ORDER BY r.Name, p.[Key];

-- 4. Check if user has specific UserInfo permissions
-- Note: Permission.Key format is typically "Resource:Action" (e.g., "UserInfo:Read")
SELECT 
    u.Id AS UserId,
    u.Email,
    r.Name AS RoleName,
    p.[Key] AS PermissionKey,
    CASE 
        WHEN p.[Key] LIKE 'UserInfo:%' THEN 'HAS UserInfo Permission'
        WHEN p.[Key] LIKE 'User:%' THEN 'HAS User Permission'
        ELSE 'Other Permission'
    END AS PermissionType
FROM Users u
INNER JOIN UserRoles ur ON u.Id = ur.UserId
INNER JOIN Roles r ON ur.RoleId = r.Id
INNER JOIN RolePermissions rp ON r.Id = rp.RoleId
INNER JOIN Permissions p ON rp.PermissionId = p.Id
WHERE u.Email = 'michael.josiah@mailinator.com'
  AND (p.[Key] LIKE 'UserInfo:%' OR p.[Key] LIKE 'User:%');

-- 5. List ALL available permissions in the system (for comparison)
SELECT 
    p.Id AS PermissionId,
    p.[Key] AS PermissionKey,
    p.Description
FROM Permissions p
WHERE p.[Key] LIKE 'UserInfo:%' OR p.[Key] LIKE 'User:%'
ORDER BY p.[Key];

-- 6. Check if SystemAdmin role exists and has permissions
SELECT 
    r.Id AS RoleId,
    r.Name AS RoleName,
    r.TenantId,
    p.[Key] AS PermissionKey,
    p.Description AS PermissionDescription
FROM Roles r
LEFT JOIN RolePermissions rp ON r.Id = rp.RoleId
LEFT JOIN Permissions p ON rp.PermissionId = p.Id
WHERE r.Name = 'SystemAdmin'
ORDER BY p.[Key];

-- 7. Complete user authentication profile
SELECT 
    u.Id AS UserId,
    u.Email,
    u.TenantId,
    u.Status,
    t.Id AS TenantExists,
    t.Name AS TenantName,
    COUNT(DISTINCT ur.RoleId) AS RoleCount,
    COUNT(DISTINCT p.Id) AS PermissionCount
FROM Users u
LEFT JOIN Tenants t ON u.TenantId = t.Id
LEFT JOIN UserRoles ur ON u.Id = ur.UserId
LEFT JOIN RolePermissions rp ON ur.RoleId = rp.RoleId
LEFT JOIN Permissions p ON rp.PermissionId = p.Id
WHERE u.Email = 'michael.josiah@mailinator.com'
GROUP BY u.Id, u.Email, u.TenantId, u.Status, t.Id, t.Name;

-- ============================================================================
-- SECTION C: QUICK CHECKS
-- ============================================================================

-- Quick Check 1: How many tenants exist?
SELECT COUNT(*) AS TenantCount FROM Tenants;

-- Quick Check 2: How many users exist?
SELECT COUNT(*) AS UserCount FROM Users;

-- Quick Check 3: List all tenants
SELECT 
    Id AS TenantId,
    Name AS TenantName,
    Status,
    Environment,
    DefaultCurrency,
    CreatedAt
FROM Tenants
ORDER BY CreatedAt DESC;

-- Quick Check 4: List all users with their tenants
SELECT 
    u.Id AS UserId,
    u.Email,
    u.Status AS UserStatus,
    u.ExternalIssuer,
    u.ExternalSubject,
    t.Name AS TenantName,
    t.Status AS TenantStatus,
    u.CreatedAt
FROM Users u
LEFT JOIN Tenants t ON u.TenantId = t.Id
ORDER BY u.CreatedAt DESC;

-- Quick Check 5: List all roles and how many users have them
SELECT 
    r.Name AS RoleName,
    r.TenantId,
    t.Name AS TenantName,
    COUNT(DISTINCT ur.UserId) AS UserCount,
    COUNT(DISTINCT rp.PermissionId) AS PermissionCount
FROM Roles r
LEFT JOIN Tenants t ON r.TenantId = t.Id
LEFT JOIN UserRoles ur ON r.Id = ur.RoleId
LEFT JOIN RolePermissions rp ON r.Id = rp.RoleId
GROUP BY r.Id, r.Name, r.TenantId, t.Name
ORDER BY r.Name;

-- ============================================================================
-- USAGE NOTES
-- ============================================================================
-- 
-- SECTION A (Reset Database):
--   - Uncomment the DELETE statements to wipe all data
--   - Run in order (respects foreign key constraints)
--   - Use when you want to test bootstrap from scratch
--
-- SECTION B (Debug User):
--   - Query #1: Find user by email
--   - Query #1b: List ALL users (if email is NULL)
--   - Query #1c: Find user by ExternalSubject from JWT
--   - Query #2-7: Check roles, permissions, and auth profile
--
-- SECTION C (Quick Checks):
--   - Quick summaries of tenants, users, and roles
--   - Use to verify bootstrap succeeded
--
-- ============================================================================
