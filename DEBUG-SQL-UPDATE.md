# Debug User Roles SQL - Updated with Bootstrap Reset

## What's New

I've updated `debug-user-roles.sql` to include **three sections**:

### Section A: Reset Database for Fresh Bootstrap
Complete DELETE script to wipe all data and start fresh with bootstrap.

**Features:**
- ✅ Deletes data in correct order (respects foreign key constraints)
- ✅ Covers ALL tables in the database (50+ tables)
- ✅ Commented out by default (safety - must manually uncomment)
- ✅ Includes PRINT statements to show progress
- ⚠️ **WARNING messages** to prevent accidental data loss

**Tables Deleted (in order):**
1. Identity & Users (UserRoles, Users, Roles, Permissions, etc.)
2. Settings & Reference Data
3. Audit & Compliance logs
4. AI & Agent data (optional)
5. Business data - Invoices, Orders, Payments (optional)
6. Party & Profile data
7. Personal Finance (optional)
8. Catalog (optional)
9. Partners & Pricing (optional)
10. Operations & Notifications
11. **Tenants (LAST!)**

### Section B: Debug User Roles and Permissions
All the original queries to debug user authentication issues.

**Queries Included:**
- Query #1: Find user by email
- Query #1b: List ALL users (if email is NULL)
- Query #1c: Find user by ExternalSubject
- Query #2: Get all roles for user
- Query #3: Get all permissions for user
- Query #4: Check specific UserInfo permissions
- Query #5: List all available permissions
- Query #6: Check SystemAdmin role
- Query #7: Complete auth profile

### Section C: Quick Checks
New quick summary queries to verify bootstrap success.

**Quick Checks:**
1. How many tenants exist?
2. How many users exist?
3. List all tenants
4. List all users with tenants
5. List all roles with user counts

## How to Use

### To Reset Database and Bootstrap Fresh

1. **Open the SQL file** in SQL Server Management Studio or Azure Data Studio

2. **Scroll to Section A** and **UNCOMMENT** the DELETE block:
   ```sql
   /* <-- REMOVE THIS LINE
   
   DELETE FROM UserRoles;
   DELETE FROM RolePermissions;
   -- ... etc
   
   */ <-- AND REMOVE THIS LINE
   ```

3. **Execute Section A** - All data will be deleted

4. **Configure Auth0** - Add email claim to JWT (see `auth0-email-claim-setup.md`)

5. **Run Bootstrap**:
   ```bash
   POST https://localhost:5001/bootstrap
   Authorization: Bearer <jwt-with-email-claim>
   ```

6. **Verify** using Section C (Quick Checks):
   ```sql
   -- Quick Check 1: Should return 1
   SELECT COUNT(*) AS TenantCount FROM Tenants;
   
   -- Quick Check 2: Should return 1
   SELECT COUNT(*) AS UserCount FROM Users;
   
   -- Quick Check 4: Should show your user with email populated
   SELECT u.Id, u.Email, u.Status, t.Name AS TenantName
   FROM Users u
   LEFT JOIN Tenants t ON u.TenantId = t.Id;
   ```

### To Debug Existing User

Just run **Section B** queries with your email or ExternalSubject.

### To Check System State

Run **Section C** Quick Checks to see overview of tenants, users, and roles.

## Safety Features

### 1. Commented Out by Default
The DELETE block is wrapped in `/* */` comments - **must manually uncomment** to execute.

### 2. Warning Messages
Multiple ⚠️ warnings at the top:
```sql
-- ⚠️ WARNING: These DELETE statements will remove ALL data!
-- ⚠️ Only run this when you want to start bootstrap from scratch!
-- ⚠️ CANNOT BE UNDONE - backup your database first if needed!
```

### 3. Progress Feedback
PRINT statements show which tables are being deleted:
```sql
PRINT 'Deleting UserRoles...';
DELETE FROM UserRoles;

PRINT 'Deleting Users...';
DELETE FROM Users;
```

### 4. Success Confirmation
Final message confirms completion:
```sql
PRINT '✅ All data deleted successfully! Database is ready for fresh bootstrap.';
PRINT '   Run POST /bootstrap to create a new tenant and user.';
```

## Example Workflow

### Complete Fresh Bootstrap Test

```sql
-- 1. Uncomment and run Section A (delete all data)
/*
DELETE FROM UserRoles;
DELETE FROM Users;
-- ... etc
DELETE FROM Tenants;
*/

-- 2. Verify database is empty
SELECT COUNT(*) AS TenantCount FROM Tenants;  -- Should return 0
SELECT COUNT(*) AS UserCount FROM Users;      -- Should return 0

-- 3. (Outside SQL) Configure Auth0 with email claim

-- 4. (Outside SQL) Run POST /bootstrap with JWT

-- 5. Verify bootstrap succeeded
SELECT COUNT(*) AS TenantCount FROM Tenants;  -- Should return 1
SELECT COUNT(*) AS UserCount FROM Users;      -- Should return 1

-- 6. Check user has email
SELECT 
    u.Id,
    u.Email,  -- Should be populated! ✅
    u.Status,
    u.ExternalSubject,
    t.Name AS TenantName
FROM Users u
LEFT JOIN Tenants t ON u.TenantId = t.Id;

-- 7. Check user has PlatformAdmin role
SELECT 
    u.Email,
    r.Name AS RoleName,
    r.TenantId AS RoleTenantId
FROM Users u
INNER JOIN UserRoles ur ON u.Id = ur.UserId
INNER JOIN Roles r ON ur.RoleId = r.Id;
-- Should show: Email | PlatformAdmin | 00000000-0000-0000-0000-000000000000
```

## Tables Not Deleted (Intentionally)

Some tables are **NOT** deleted because they are managed by Entity Framework or the system:

- `__EFMigrationsHistory` - EF Core migration tracking (leave alone)
- Any system tables like `sysdiagrams` (if present)

## Optional Sections

You can **comment out** certain DELETE sections if you want to keep that data:

### Keep AI/Agent Configuration
```sql
-- DELETE FROM AiFeedbacks;
-- DELETE FROM AiRuns;
-- ... etc (comment out entire AI section)
```

### Keep Business Data
```sql
-- DELETE FROM Invoices;
-- DELETE FROM Orders;
-- ... etc (comment out entire business section)
```

### Keep Catalog/Partner Configuration
```sql
-- DELETE FROM CatalogBillers;
-- DELETE FROM Partners;
-- ... etc
```

**But always delete:**
- UserRoles
- Users
- Roles
- Tenants

These MUST be deleted for fresh bootstrap!

## File Location

📄 `C:\Users\mjosi\source\repos\aonik\debug-user-roles.sql`

## Summary

✅ Updated `debug-user-roles.sql` with:
- **Section A**: Complete database reset script (commented out for safety)
- **Section B**: Original user debug queries
- **Section C**: Quick check queries

Now you can easily reset the database and start the bootstrap process fresh to verify email storage!
