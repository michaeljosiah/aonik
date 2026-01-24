-- ============================================================================
-- COMPREHENSIVE USER PERMISSION VERIFICATION
-- ============================================================================
-- This script verifies that users have permissions through their role assignments
-- Run this after bootstrap to confirm everything is working correctly
-- ============================================================================

PRINT '========================================';
PRINT 'STEP 1: Check if user exists';
PRINT '========================================';
SELECT 
    u.Id AS UserId,
    u.Email,
    u.Status,
    u.ExternalIssuer,
    u.TenantId,
    u.CreatedAt
FROM Users u
WHERE u.Email = 'michael.josiah@mailinator.com';

PRINT '';
PRINT '========================================';
PRINT 'STEP 2: Check if PlatformAdmin role exists';
PRINT '========================================';
SELECT 
    r.Id AS RoleId,
    r.Name,
    r.TenantId,
    r.CreatedAt
FROM Roles r
WHERE r.Name = 'PlatformAdmin';

PRINT '';
PRINT '========================================';
PRINT 'STEP 3: Check UserRole assignment';
PRINT '========================================';
SELECT 
    ur.Id AS UserRoleId,
    u.Email AS UserEmail,
    r.Name AS RoleName,
    ur.UserId,
    ur.RoleId
FROM UserRoles ur
JOIN Users u ON ur.UserId = u.Id
JOIN Roles r ON ur.RoleId = r.Id
WHERE u.Email = 'michael.josiah@mailinator.com';

PRINT '';
PRINT '========================================';
PRINT 'STEP 4: Check UserInfo permissions exist';
PRINT '========================================';
SELECT 
    p.Id AS PermissionId,
    p.[Key] AS PermissionKey,
    p.Description
FROM Permissions p
WHERE p.[Key] IN ('UserInfo.Read', 'UserInfo.Update');

PRINT '';
PRINT '========================================';
PRINT 'STEP 5: Check RolePermission assignments for PlatformAdmin';
PRINT '========================================';
SELECT 
    r.Name AS RoleName,
    p.[Key] AS PermissionKey,
    p.Description AS PermissionDescription,
    rp.Id AS RolePermissionId
FROM Roles r
JOIN RolePermissions rp ON r.Id = rp.RoleId
JOIN Permissions p ON rp.PermissionId = p.Id
WHERE r.Name = 'PlatformAdmin'
ORDER BY p.[Key];

PRINT '';
PRINT '========================================';
PRINT 'STEP 6: FINAL CHECK - User effective permissions';
PRINT '========================================';
PRINT 'This shows ALL permissions the user has through their role(s)';
PRINT '';

SELECT 
    u.Email AS UserEmail,
    r.Name AS RoleName,
    p.[Key] AS PermissionKey,
    p.Description AS PermissionDescription,
    CASE 
        WHEN p.[Key] LIKE 'UserInfo.%' THEN '*** UserInfo Permission ***'
        ELSE ''
    END AS HighlightUserInfo
FROM Users u
JOIN UserRoles ur ON u.Id = ur.UserId
JOIN Roles r ON ur.RoleId = r.Id
JOIN RolePermissions rp ON r.Id = rp.RoleId
JOIN Permissions p ON rp.PermissionId = p.Id
WHERE u.Email = 'michael.josiah@mailinator.com'
ORDER BY p.[Key];

PRINT '';
PRINT '========================================';
PRINT 'STEP 7: Count total permissions for user';
PRINT '========================================';
SELECT 
    u.Email,
    COUNT(DISTINCT p.Id) AS TotalPermissions,
    COUNT(DISTINCT CASE WHEN p.[Key] LIKE 'UserInfo.%' THEN p.Id END) AS UserInfoPermissions
FROM Users u
JOIN UserRoles ur ON u.Id = ur.UserId
JOIN Roles r ON ur.RoleId = r.Id
JOIN RolePermissions rp ON r.Id = rp.RoleId
JOIN Permissions p ON rp.PermissionId = p.Id
WHERE u.Email = 'michael.josiah@mailinator.com'
GROUP BY u.Email;

PRINT '';
PRINT '========================================';
PRINT 'EXPECTED RESULTS:';
PRINT '========================================';
PRINT 'Step 1: Should show 1 user with email michael.josiah@mailinator.com';
PRINT 'Step 2: Should show 1 PlatformAdmin role';
PRINT 'Step 3: Should show 1 UserRole linking the user to PlatformAdmin';
PRINT 'Step 4: Should show 2 permissions (UserInfo.Read, UserInfo.Update)';
PRINT 'Step 5: Should show 27 permissions assigned to PlatformAdmin role';
PRINT 'Step 6: Should show 27 rows with user having permissions through PlatformAdmin';
PRINT 'Step 7: Should show TotalPermissions=27, UserInfoPermissions=2';
PRINT '';
PRINT 'If any step returns 0 rows or wrong counts, there is a problem!';
PRINT '========================================';
