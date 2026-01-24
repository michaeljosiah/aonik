-- DEBUG: Check what permissions are being looked up
-- This simulates what EnsureGlobalPlatformAdminAsync does

DECLARE @permissionKeys TABLE ([Key] NVARCHAR(255));

INSERT INTO @permissionKeys VALUES 
('Tenants.Read'),
('Tenants.Write'),
('Users.Read'),
('Users.Invite'),
('Users.Manage'),
('Users.Deactivate'),
('UserInfo.Read'),
('UserInfo.Update'),
('Settings.Read'),
('Settings.Write'),
('Roles.Read'),
('Roles.Create'),
('Roles.Update'),
('Roles.Delete'),
('Ledger.Read'),
('Ledger.Write'),
('Ledger.Reconcile'),
('Payment.Read'),
('Payment.Create'),
('Payment.Capture'),
('Payment.Cancel'),
('Payment.Refund'),
('Invoice.Read'),
('Invoice.Create'),
('Invoice.Update'),
('Invoice.Delete'),
('Invoice.Issue');

PRINT '========================================';
PRINT 'STEP 1: All permissions that SHOULD exist';
PRINT '========================================';
SELECT * FROM @permissionKeys;

PRINT '';
PRINT '========================================';
PRINT 'STEP 2: Permissions that ACTUALLY exist in database';
PRINT '========================================';
SELECT p.Id, p.[Key], p.Description
FROM Permissions p
WHERE p.[Key] IN (SELECT [Key] FROM @permissionKeys)
ORDER BY p.[Key];

PRINT '';
PRINT '========================================';
PRINT 'STEP 3: Missing permissions (in code but not in DB)';
PRINT '========================================';
SELECT pk.[Key] AS MissingPermissionKey
FROM @permissionKeys pk
WHERE pk.[Key] NOT IN (SELECT p.[Key] FROM Permissions p)
ORDER BY pk.[Key];

PRINT '';
PRINT '========================================';
PRINT 'STEP 4: Count comparison';
PRINT '========================================';
SELECT 
    (SELECT COUNT(*) FROM @permissionKeys) AS ExpectedCount,
    (SELECT COUNT(*) FROM Permissions WHERE [Key] IN (SELECT [Key] FROM @permissionKeys)) AS ActualCount;

PRINT '';
PRINT '========================================';
PRINT 'DIAGNOSIS:';
PRINT '========================================';
PRINT 'If ActualCount < ExpectedCount, permissions were not seeded properly';
PRINT 'If ActualCount = 0, IdentitySeedService.SeedAsync() was not called';
PRINT 'Expected: 27 permissions total (25 original + 2 UserInfo)';
PRINT '';

-- Check if the app has started at all
PRINT '========================================';
PRINT 'STEP 5: Check if ANY permissions exist';
PRINT '========================================';
SELECT COUNT(*) AS TotalPermissionsInDatabase FROM Permissions;

PRINT '';
PRINT 'If TotalPermissionsInDatabase = 0, the seed service never ran!';
PRINT '========================================';
