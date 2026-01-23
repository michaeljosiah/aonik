# UserInfo Permission Fix - Complete Solution

## Problem Summary
UserInfo endpoints were returning **401 Unauthorized** even for users with PlatformAdmin role because the `UserInfo.Read` and `UserInfo.Update` permissions **did not exist** in the system.

## Root Cause
The permissions seeding service (`IdentitySeedService`) did not include UserInfo permissions, and therefore the PlatformAdmin role provisioning logic couldn't assign them.

## Solution Implemented

### Change #1: Added UserInfo Permissions to Seed
**File**: `src/Aonik.Infrastructure/Persistence/Seed/IdentitySeedService.cs` (lines 76-78)

```csharp
// UserInfo permissions (for user profile endpoints)
new Permission { Key = "UserInfo.Read", Description = "View user information and profile" },
new Permission { Key = "UserInfo.Update", Description = "Update user information and profile" },
```

This ensures the permissions are created in the database when the application starts.

### Change #2: Assigned UserInfo Permissions to PlatformAdmin
**File**: `src/Aonik.Application/Services/Identity/Provisioning/TenantProvisioner.cs` (lines 331-332)

```csharp
var permissionKeys = new[]
{
    "Tenants.Read",
    "Tenants.Write",
    "Users.Read",
    "Users.Invite",
    "Users.Manage",
    "Users.Deactivate",
    "UserInfo.Read",          // ← ADDED
    "UserInfo.Update",        // ← ADDED
    "Settings.Read",
    // ... rest of permissions
};
```

This ensures the PlatformAdmin role gets the UserInfo permissions during bootstrap.

## How to Apply This Fix

### Step 1: Stop Running Applications
Stop the API and Worker projects in Visual Studio (they're currently locking the DLL files).

### Step 2: Clear Existing Data (Optional but Recommended)
Run the database reset script to start fresh:

```sql
-- From debug-user-roles.sql, Section A (Database Reset)
-- This deletes all data so bootstrap can recreate everything correctly
DELETE FROM [AiRunStepToolCalls];
DELETE FROM [AiRunSteps];
-- ... (50+ DELETE statements - see debug-user-roles.sql)
```

**Why?** If you don't reset, you'll need to manually insert the new permissions and assign them to existing roles.

### Step 3: Configure Auth0 Email Claim
Before bootstrapping, ensure Auth0 includes email in JWT tokens. Add this Auth0 Action:

```javascript
exports.onExecutePostLogin = async (event, api) => {
  if (event.user.email) {
    // Option 1: Standard claim
    api.accessToken.setCustomClaim('email', event.user.email);
    
    // OR Option 2: Namespaced claim (recommended)
    api.accessToken.setCustomClaim('https://aonik.app/email', event.user.email);
  }
};
```

See `auth0-email-claim-setup.md` for detailed instructions.

### Step 4: Start Application
```bash
dotnet run --project src/Aonik.Api
```

**What happens on startup:**
1. `IdentitySeedService.SeedAsync()` runs (from `Program.cs` lines 60-62)
2. Creates all permissions including `UserInfo.Read` and `UserInfo.Update`
3. Application starts and listens for requests

### Step 5: Bootstrap Tenant
```bash
POST https://localhost:5001/bootstrap
Authorization: Bearer <your_auth0_jwt>
```

**What happens during bootstrap:**
1. User email extracted from JWT (using `ClaimsEmailResolver.GetEmail()`)
2. User record created with email stored
3. First tenant created
4. PlatformAdmin role created
5. **UserInfo.Read and UserInfo.Update permissions assigned to PlatformAdmin**
6. User assigned PlatformAdmin role

### Step 6: Test UserInfo Endpoint
```bash
GET https://localhost:5001/api/user-profile/me
Authorization: Bearer <your_auth0_jwt>
```

**Expected result:** 200 OK with user profile data

## Verification Queries

### Verify Permissions Exist
```sql
SELECT * FROM Permissions WHERE [Key] IN ('UserInfo.Read', 'UserInfo.Update');
```

**Expected:** 2 rows

### Verify PlatformAdmin Has UserInfo Permissions
```sql
SELECT r.Name, p.[Key]
FROM Roles r
JOIN RolePermissions rp ON r.Id = rp.RoleId
JOIN Permissions p ON rp.PermissionId = p.Id
WHERE r.Name = 'PlatformAdmin' AND p.[Key] LIKE 'UserInfo.%';
```

**Expected:** 2 rows (UserInfo.Read, UserInfo.Update)

### Verify User Has UserInfo Permissions Through Role
```sql
SELECT u.Email, r.Name, p.[Key]
FROM Users u
JOIN UserRoles ur ON u.Id = ur.UserId
JOIN Roles r ON ur.RoleId = r.Id
JOIN RolePermissions rp ON r.Id = rp.RoleId
JOIN Permissions p ON rp.PermissionId = p.Id
WHERE u.Email = 'michael.josiah@mailinator.com' AND p.[Key] LIKE 'UserInfo.%';
```

**Expected:** 2 rows showing user has UserInfo.Read and UserInfo.Update via PlatformAdmin role

## Why This Fix Works

### Before Fix:
1. JWT validates ✅
2. User exists in database ✅
3. User has PlatformAdmin role ✅
4. PlatformAdmin role has many permissions ✅
5. **UserInfo.Read permission DOES NOT EXIST ❌**
6. Authorization check fails → 401 Unauthorized

### After Fix:
1. JWT validates ✅
2. User exists in database with email stored ✅
3. User has PlatformAdmin role ✅
4. PlatformAdmin role has UserInfo.Read permission ✅
5. **UserInfo.Read permission EXISTS ✅**
6. Authorization check succeeds → 200 OK

## Related Fixes

This fix is part of a series of authentication/authorization fixes:

1. **Email Storage Fix** (`SUMMARY-auth0-email-fix.md`) - Fixed email extraction from JWT
2. **Bootstrap Email Fix** (`BOOTSTRAP-EMAIL-CONFIRMATION.md`) - Fixed email storage during bootstrap
3. **SQL Keyword Fix** (`SQL-KEYWORD-FIX.md`) - Fixed SQL queries for debugging
4. **UserInfo Permission Fix** (this document) - Added missing UserInfo permissions

## Technical Details

### Permission Seeding is Idempotent
The `SeedPermissionsAsync` method checks for existing permissions before inserting:

```csharp
var existingKeys = await _dbContext.Permissions.Select(p => p.Key).ToListAsync();
var existingKeySet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
var newPermissions = permissions.Where(p => !existingKeySet.Contains(p.Key)).ToList();
```

This means:
- Safe to restart application multiple times
- Won't create duplicate permissions
- Only inserts missing permissions

### Permissions Are Global (Not Tenant-Scoped)
From `IdentitySeedService.cs` comments:
> Permissions are global (not tenant-scoped) and define what actions can be performed.
> Roles are tenant-specific and are created manually per tenant.

This means:
- UserInfo.Read exists once in the Permissions table
- Multiple tenants can have roles that reference this permission
- Permission definitions are shared across all tenants

### Authorization Flow
```
HTTP Request with JWT
    ↓
JWT Validation (Auth0)
    ↓
User Lookup (by external provider ID)
    ↓
[Policy: UserInfo.Read]
    ↓
Query: User → UserRoles → Roles → RolePermissions → Permissions
    ↓
Permission Found? → 200 OK
Permission Missing? → 401 Unauthorized
```

## Files Modified

1. `src/Aonik.Infrastructure/Persistence/Seed/IdentitySeedService.cs` (lines 76-78)
2. `src/Aonik.Application/Services/Identity/Provisioning/TenantProvisioner.cs` (lines 331-332)

Both files compiled successfully (build passed for these projects).

## Next Steps

1. **Stop running applications** in Visual Studio
2. **Reset database** using `debug-user-roles.sql` Section A
3. **Configure Auth0** to include email claim
4. **Start application** (permissions will seed)
5. **Run bootstrap** (user will get PlatformAdmin with UserInfo permissions)
6. **Test UserInfo endpoint** (should return 200 OK)

## Success Criteria

✅ UserInfo.Read permission exists in Permissions table
✅ UserInfo.Update permission exists in Permissions table
✅ PlatformAdmin role has UserInfo.Read assigned
✅ PlatformAdmin role has UserInfo.Update assigned
✅ Bootstrap user has PlatformAdmin role
✅ GET /api/user-profile/me returns 200 OK (not 401)
✅ User email is stored in database (not NULL)

---

**Status**: Code changes complete and compiled successfully. Ready to test after application restart.
