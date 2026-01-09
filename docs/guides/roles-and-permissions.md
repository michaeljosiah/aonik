# Managing Roles and Permissions

This guide explains how to manage roles and assign permissions to users in AONIK. This is for **tenant administrators** who need to control what users can do within their tenant.

## Quick Start

**The 3-Step Process:**

1. **Create a Role** - Define a job function (e.g., "Accountant")
2. **Assign Permissions** - Give the role specific permissions (e.g., `Invoice.Create`)
3. **Assign Users** - Add users to the role

---

## Understanding Roles

### What is a Role?

A role is a **named collection of permissions** that represents a job function or responsibility level.

**Examples:**
- **Accountant** - Can create invoices and manage the ledger
- **Payment Processor** - Can process and capture payments
- **Manager** - Can do everything the Accountant can do, plus manage users
- **Viewer** - Can view data but not modify anything

### Why Use Roles?

**Without Roles:**
- John needs Invoice.Create, Invoice.Read, Invoice.Update, Ledger.Read, Ledger.Write
- Sarah needs Invoice.Create, Invoice.Read, Invoice.Update, Ledger.Read, Ledger.Write  
- Mike needs Invoice.Create, Invoice.Read, Invoice.Update, Ledger.Read, Ledger.Write

😫 Managing permissions for each user individually is tedious!

**With Roles:**
- Create "Accountant" role with those 5 permissions
- Assign John, Sarah, and Mike to "Accountant" role

😊 Much easier!

### Key Characteristics

- **Tenant-Scoped**: Roles exist within a tenant and don't affect other tenants
- **Reusable**: One role can be assigned to many users
- **Composable**: Users can have multiple roles (permissions combine)
- **Dynamic**: Changes to a role affect all users with that role

---

## Creating Roles

### Step 1: Plan Your Roles

Before creating roles in the system, plan out what roles your organization needs.

**Questions to Ask:**
- What are the different job functions in your organization?
- What tasks does each function need to perform?
- Should some roles have subset permissions of others?

**Example Organization:**

| Role | Responsibilities | Permissions Needed |
|------|-----------------|-------------------|
| Viewer | View-only access for reporting | Invoice.Read, Payment.Read, Ledger.Read |
| Accountant | Manage invoices and ledger | Invoice.*, Ledger.* |
| Payment Processor | Handle payment operations | Payment.*, Invoice.Read |
| Manager | Supervise team, all operations | All business permissions + Users.Manage |
| Admin | Full tenant control | All permissions |

### Step 2: Map Permissions

For each role, list the specific permissions needed. Use the [Permissions Reference](../reference/permissions.md) to find available permissions.

**Example: "Accountant" Role**

Needs:
- ✅ `Invoice.Create` - Create invoices
- ✅ `Invoice.Read` - View invoices
- ✅ `Invoice.Update` - Edit invoices  
- ✅ `Invoice.Issue` - Issue invoices to customers
- ✅ `Ledger.Read` - View ledger
- ✅ `Ledger.Write` - Post to ledger
- ❌ `Payment.Capture` - Should NOT process payments
- ❌ `Users.Manage` - Should NOT manage team

### Step 3: Create Role in Database

**Note:** Role management UI is not yet implemented. Use SQL or create via seeding script.

**Example SQL:**

```sql
-- 1. Create the role
INSERT INTO Roles (Id, TenantId, Name, Description, CreatedAt, UpdatedAt)
VALUES (
    NEWID(),
    '550e8400-e29b-41d4-a716-446655440000',  -- Your tenant ID
    'Accountant',
    'Manages invoices and ledger entries',
    GETUTCDATE(),
    GETUTCDATE()
);

-- 2. Get the role ID
DECLARE @RoleId UNIQUEIDENTIFIER = (
    SELECT Id FROM Roles 
    WHERE TenantId = '550e8400-e29b-41d4-a716-446655440000' 
    AND Name = 'Accountant'
);

-- 3. Assign permissions to the role
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT @RoleId, Id FROM Permissions WHERE [Key] IN (
    'Invoice.Create',
    'Invoice.Read',
    'Invoice.Update',
    'Invoice.Issue',
    'Ledger.Read',
    'Ledger.Write'
);
```

---

## Assigning Users to Roles

### Just-In-Time Provisioning

When a user logs in for the first time, AONIK automatically creates a user record with **zero permissions**. You must assign them a role before they can do anything.

### Assign User to Role (SQL)

**Note:** User management UI is not yet implemented. Use SQL.

```sql
-- Find the user (by email)
DECLARE @UserId UNIQUEIDENTIFIER = (
    SELECT Id FROM Users 
    WHERE TenantId = '550e8400-e29b-41d4-a716-446655440000'
    AND Email = 'john@example.com'
);

-- Find the role
DECLARE @RoleId UNIQUEIDENTIFIER = (
    SELECT Id FROM Roles 
    WHERE TenantId = '550e8400-e29b-41d4-a716-446655440000'
    AND Name = 'Accountant'
);

-- Assign user to role
INSERT INTO UserRoles (UserId, RoleId)
VALUES (@UserId, @RoleId);
```

### Assign Multiple Roles

Users can have multiple roles. Their effective permissions are the **union** of all permissions from all their roles.

**Example:**

- John has "Accountant" role (Invoice.*, Ledger.*)
- John also has "Payment Processor" role (Payment.*)
- John's effective permissions = Invoice.* + Ledger.* + Payment.*

```sql
-- Assign second role
INSERT INTO UserRoles (UserId, RoleId)
SELECT @UserId, Id FROM Roles 
WHERE TenantId = '550e8400-e29b-41d4-a716-446655440000'
AND Name IN ('Accountant', 'Payment Processor');
```

---

## Modifying Roles

### Adding Permissions to a Role

```sql
-- Add Invoice.Delete permission to Accountant role
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT 
    r.Id,
    p.Id
FROM Roles r
CROSS JOIN Permissions p
WHERE r.TenantId = '550e8400-e29b-41d4-a716-446655440000'
  AND r.Name = 'Accountant'
  AND p.[Key] = 'Invoice.Delete';
```

**Effect:** All users with "Accountant" role immediately gain `Invoice.Delete` permission (on their next request).

### Removing Permissions from a Role

```sql
-- Remove Invoice.Delete permission from Accountant role
DELETE rp
FROM RolePermissions rp
INNER JOIN Roles r ON rp.RoleId = r.Id
INNER JOIN Permissions p ON rp.PermissionId = p.Id
WHERE r.TenantId = '550e8400-e29b-41d4-a716-446655440000'
  AND r.Name = 'Accountant'
  AND p.[Key] = 'Invoice.Delete';
```

**Effect:** All users with "Accountant" role immediately lose `Invoice.Delete` permission.

### Removing a User from a Role

```sql
-- Remove John from Accountant role
DELETE ur
FROM UserRoles ur
INNER JOIN Users u ON ur.UserId = u.Id
INNER JOIN Roles r ON ur.RoleId = r.Id
WHERE u.Email = 'john@example.com'
  AND r.Name = 'Accountant'
  AND u.TenantId = '550e8400-e29b-41d4-a716-446655440000';
```

**Effect:** John loses all permissions from the "Accountant" role (but keeps permissions from other roles).

---

## Common Role Patterns

### Pattern 1: Hierarchical Roles

Create roles that build on each other:

```
Viewer (base)
  ↓ + write permissions
Accountant
  ↓ + payment permissions
Manager
  ↓ + admin permissions
Admin (full access)
```

**Implementation:** Each higher role includes all permissions from lower roles.

### Pattern 2: Functional Roles

Create roles for specific functions:

```
Invoice Manager    Payment Processor    Ledger Accountant
    ↓                    ↓                     ↓
All Invoice.*      All Payment.*         All Ledger.*
```

**Implementation:** Specialized roles for different departments.

**Combine:** A manager might have all three roles.

### Pattern 3: Temporary Elevated Access

Create roles for temporary special access:

**Example:**
- Base role: "Accountant" (normal day-to-day permissions)
- Elevated role: "Month-End Closer" (adds `Ledger.Reconcile`)
- Assign "Month-End Closer" only during closing period
- Remove after month-end closes

### Pattern 4: Approval Workflows

Use roles to implement approval chains:

**Example:**
- "Invoice Creator" role - Can create invoices (saves as Draft)
- "Invoice Approver" role - Can issue invoices (makes them active)

---

## Best Practices

### 1. Start with Few Roles

Don't create dozens of roles on day one. Start with 3-5 core roles:
- Admin
- Manager
- Standard User
- Viewer

Add more as needs emerge.

### 2. Name Roles by Function, Not by Person

❌ Bad: "Johns-Role", "Sarahs-Permissions"  
✅ Good: "Accountant", "Manager", "Viewer"

Roles should represent job functions, not individuals.

### 3. Document Role Purposes

Maintain documentation of what each role is for:

```
Role: Accountant
Purpose: Financial team members who manage invoicing and ledger
Typical Users: Finance department staff
Permissions: Invoice.*, Ledger.*, Settings.Read
Should NOT have: Payment.*, Users.*, Settings.Write
```

### 4. Review Roles Regularly

- **Quarterly:** Review all role assignments
- **When someone leaves:** Remove their user from all roles (or deactivate user)
- **When job changes:** Update role assignments
- **When adding features:** Update relevant roles with new permissions

### 5. Principle of Least Privilege

Give users the **minimum permissions** needed to do their job. It's easier to add permissions later than to take them away.

### 6. Test New Roles

Before assigning a new role to real users:
1. Create a test user
2. Assign the new role
3. Test that they can do what they need
4. Test that they can't do what they shouldn't

### 7. Log Role Changes

All role modifications should be logged:
- Who made the change
- What changed (permissions added/removed)
- When it happened
- Why it happened (ticket/request number)

**Status:** Logging not yet implemented - manually track changes

---

## Troubleshooting

### User gets 403 Forbidden after login

**Possible Causes:**

1. **User has no roles assigned**
   - Check: `SELECT * FROM UserRoles WHERE UserId = '{user-id}'`
   - Fix: Assign user to a role

2. **User's roles have no permissions**
   - Check: `SELECT * FROM RolePermissions WHERE RoleId IN (SELECT RoleId FROM UserRoles WHERE UserId = '{user-id}')`
   - Fix: Add permissions to the role

3. **Role has wrong permissions**
   - Check: Endpoint needs `Invoice.Create` but role only has `Invoice.Read`
   - Fix: Add missing permission to role

4. **User in wrong tenant**
   - Check: User trying to access Tenant A but their user record is in Tenant B
   - Fix: User needs to use correct tenant URL/claim

### User can do things they shouldn't

**Possible Causes:**

1. **User has multiple roles**
   - Check: `SELECT r.Name FROM Roles r INNER JOIN UserRoles ur ON r.Id = ur.RoleId WHERE ur.UserId = '{user-id}'`
   - Fix: Remove user from over-privileged role

2. **Role has too many permissions**
   - Check: `SELECT p.[Key] FROM Permissions p INNER JOIN RolePermissions rp ON p.Id = rp.PermissionId WHERE rp.RoleId = '{role-id}'`
   - Fix: Remove excessive permissions from role

### Permission changes not taking effect

**Possible Causes:**

1. **User using old access token**
   - Permissions are checked on each request, but token might be cached client-side
   - Fix: User should refresh the page / re-login

2. **Wrong tenant context**
   - User authenticated to Tenant A but trying to access Tenant B resources
   - Fix: Check tenant routing configuration

3. **Role assigned in different tenant**
   - User has role in Tenant A but trying to use it in Tenant B
   - Roles don't cross tenant boundaries
   - Fix: Create role in correct tenant and assign user

---

## Coming Soon

The following features are planned but not yet implemented:

- **Role Management UI** - Create and edit roles through web interface
- **User Management UI** - Assign users to roles through web interface
- **Role Templates** - Pre-configured role templates for common scenarios
- **Role History** - Track role assignment changes over time
- **Bulk Operations** - Assign multiple users to roles at once
- **Role Approval Workflows** - Require approval for sensitive role assignments

---

## See Also

- [Permissions Reference](../reference/permissions.md) - Complete list of permissions
- [Authentication Overview](../features/authentication-authorization.md) - How auth works
- [Azure AD Setup](authentication-azure-ad.md) - Configure Azure AD
- [Auth0 Setup](authentication-auth0.md) - Configure Auth0
- [Troubleshooting Authentication](authentication-troubleshooting.md) - Common issues

---

**Last Updated:** January 9, 2025  
**Status:** Role/User management UI not yet implemented - use SQL for now
