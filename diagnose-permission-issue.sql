-- ============================================================================
-- CRITICAL DIAGNOSTIC: Why are permissions not being saved?
-- ============================================================================

PRINT '========================================';
PRINT 'TEST 1: Do ANY permissions exist?';
PRINT '========================================';
SELECT COUNT(*) AS TotalPermissions FROM Permissions;
PRINT 'Expected: At least 27 (if seed ran)';
PRINT 'If 0: IdentitySeedService.SeedAsync() never executed!';
PRINT '';

PRINT '========================================';
PRINT 'TEST 2: Do ANY roles exist?';
PRINT '========================================';
SELECT COUNT(*) AS TotalRoles, COUNT(CASE WHEN Name = 'PlatformAdmin' THEN 1 END) AS PlatformAdminCount
FROM Roles;
PRINT 'Expected: At least 1 role (PlatformAdmin)';
PRINT 'If 0: Bootstrap never ran OR TenantProvisioner failed';
PRINT '';

PRINT '========================================';
PRINT 'TEST 3: Do ANY role permissions exist?';
PRINT '========================================';
SELECT COUNT(*) AS TotalRolePermissions FROM RolePermissions;
PRINT 'Expected: At least 27 (if PlatformAdmin was properly set up)';
PRINT 'If 0: EnsureGlobalPlatformAdminAsync() returned early OR failed';
PRINT '';

PRINT '========================================';
PRINT 'TEST 4: Do ANY user roles exist?';
PRINT '========================================';
SELECT 
    COUNT(*) AS TotalUserRoles,
    COUNT(DISTINCT ur.UserId) AS UsersWithRoles,
    COUNT(DISTINCT ur.RoleId) AS RolesAssigned
FROM UserRoles ur;
PRINT 'Expected: At least 1 (bootstrap user assigned to PlatformAdmin)';
PRINT 'If 0: EnsurePlatformAdminRoleAsync() in BootstrapService failed';
PRINT '';

PRINT '========================================';
PRINT 'TEST 5: Show all tables row counts';
PRINT '========================================';
SELECT 
    'Permissions' AS TableName, 
    COUNT(*) AS RowCount 
FROM Permissions
UNION ALL
SELECT 'Roles', COUNT(*) FROM Roles
UNION ALL
SELECT 'RolePermissions', COUNT(*) FROM RolePermissions
UNION ALL
SELECT 'Users', COUNT(*) FROM Users
UNION ALL
SELECT 'UserRoles', COUNT(*) FROM UserRoles
UNION ALL
SELECT 'Tenants', COUNT(*) FROM Tenants;

PRINT '';
PRINT '========================================';
PRINT 'TEST 6: Check for errors in recent data';
PRINT '========================================';
SELECT TOP 10
    EventName,
    EntityType,
    TenantId,
    UserId,
    Timestamp,
    Details
FROM AuditLogs
ORDER BY Timestamp DESC;
PRINT 'Look for any error events or missing TenantProvisioned/UserRoleAssigned events';
PRINT '';

PRINT '========================================';
PRINT 'DIAGNOSIS GUIDE:';
PRINT '========================================';
PRINT 'Scenario A: Permissions = 0';
PRINT '  → App never started OR IdentitySeedService.SeedAsync() failed';
PRINT '  → Check application logs for startup errors';
PRINT '  → Solution: Restart the API project';
PRINT '';
PRINT 'Scenario B: Permissions > 0 but RolePermissions = 0';
PRINT '  → Permissions seeded but never assigned to PlatformAdmin role';
PRINT '  → Check: Did line 358 return 0? (permissions.Count == 0)';
PRINT '  → This means the WHERE clause did not match any permissions';
PRINT '  → Possible cause: Case sensitivity or exact string match issue';
PRINT '';
PRINT 'Scenario C: Permissions > 0, RolePermissions > 0, but UserRoles = 0';
PRINT '  → Everything set up except user assignment failed';
PRINT '  → Check BootstrapService.EnsurePlatformAdminRoleAsync()';
PRINT '';
PRINT 'Scenario D: All counts > 0 but UserInfo permissions missing';
PRINT '  → Old code ran before the fix';
PRINT '  → Solution: Delete RolePermissions and run bootstrap again';
PRINT '========================================';
