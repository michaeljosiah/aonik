# Permissions Reference

This document lists all permissions in the AONIK system. Permissions are **global** (not tenant-specific) and define specific actions users can perform.

## Understanding Permissions

### What is a Permission?

A permission is a single, atomic action in the system. Examples:
- `Invoice.Create` - Create a new invoice
- `Payment.Read` - View payment information
- `Ledger.Write` - Modify ledger accounts

### Key Concepts

**Atomic:** Each permission represents one specific action, not a group of actions.

**Global:** Permissions are defined once for the entire system, not per tenant.

**Database-Backed:** Permissions are stored in the `Permissions` table and seeded on startup.

**Role-Assigned:** Users don't get permissions directly - they get roles, which contain permissions.

### Permission Naming Convention

```
{Resource}.{Action}
```

Examples:
- `Invoice.Create` - Create invoices
- `Payment.Read` - Read payment data  
- `Ledger.Write` - Write to ledger

**Common Actions:**
- `Create` - Create new records
- `Read` - View existing records
- `Update` - Modify existing records
- `Delete` - Delete records
- `{SpecialAction}` - Resource-specific actions (e.g., `Invoice.Issue`, `Payment.Capture`)

---

## Complete Permissions List

### 📄 Invoice Permissions

Used for invoicing and billing operations.

| Permission | Description | Endpoints Using This |
|------------|-------------|---------------------|
| `Invoice.Create` | Create new invoices | `POST /billing/invoices` |
| `Invoice.Read` | View invoice details | `GET /billing/invoices/{id}`<br>`POST /ai/invoices/{id}/insight` |
| `Invoice.Update` | Edit existing invoices | `PATCH /billing/invoices/{id}` *(when implemented)* |
| `Invoice.Delete` | Delete invoices | `DELETE /billing/invoices/{id}` *(when implemented)* |
| `Invoice.Issue` | Issue draft invoices to customers | `POST /billing/invoices/{id}/issue` *(when implemented)* |

**Typical Role Assignments:**
- **Accountant:** `Invoice.Create`, `Invoice.Read`, `Invoice.Update`, `Invoice.Issue`
- **Manager:** All Invoice permissions
- **Viewer:** `Invoice.Read` only

**Security Notes:**
- `Invoice.Delete` should be restricted (consider soft deletes only)
- `Invoice.Issue` sends invoices to customers - audit this action
- Consider separating draft vs. issued invoice permissions for sensitive environments

---

### 💳 Payment Permissions

Used for payment processing operations.

| Permission | Description | Endpoints Using This |
|------------|-------------|---------------------|
| `Payment.Create` | Create payment intents | `POST /payments/intents` |
| `Payment.Read` | View payment information | `GET /payments/intents/{id}` |
| `Payment.Capture` | Capture authorized payments | `POST /payments/intents/{id}/capture` |
| `Payment.Cancel` | Cancel pending payments | `POST /payments/intents/{id}/cancel` |
| `Payment.Refund` | Issue refunds to customers | `POST /payments/intents/{id}/refund` *(when implemented)* |

**Typical Role Assignments:**
- **Payment Processor:** `Payment.Create`, `Payment.Read`, `Payment.Capture`, `Payment.Cancel`
- **Customer Service:** `Payment.Read`, `Payment.Refund`
- **Manager:** All Payment permissions
- **Viewer:** `Payment.Read` only

**Security Notes:**
- `Payment.Refund` has financial impact - log all refunds with reason
- `Payment.Capture` finalizes charges - implement approval workflows for large amounts
- Consider implementing daily/transaction limits per user

---

### 📒 Ledger Permissions

Used for general ledger and accounting operations.

| Permission | Description | Endpoints Using This |
|------------|-------------|---------------------|
| `Ledger.Read` | View ledger accounts and entries | `GET /ledger/accounts`<br>`GET /ledger/accounts/{id}`<br>`GET /ledger/journal-entries` |
| `Ledger.Write` | Create/modify ledger accounts and journal entries | `POST /ledger/accounts`<br>`POST /ledger/journal-entries`<br>`PATCH /ledger/accounts/{id}` |
| `Ledger.Reconcile` | Reconcile ledger accounts | `POST /ledger/accounts/{id}/reconcile` *(when implemented)* |

**Typical Role Assignments:**
- **Bookkeeper:** `Ledger.Read`, `Ledger.Write`
- **Accountant:** `Ledger.Read`, `Ledger.Write`, `Ledger.Reconcile`
- **Auditor:** `Ledger.Read` only (no write access)
- **Manager:** All Ledger permissions

**Security Notes:**
- Ledger modifications are permanent - implement approval workflows
- `Ledger.Reconcile` should be restricted to trained accounting staff
- Consider read-only access for most users (use `Ledger.Read` liberally)

**Why only Read/Write/Reconcile?**
- Ledger operations are complex and interconnected
- `Ledger.Write` covers both creating and modifying (changes are append-only in practice)
- Granular CRUD permissions would be overly complex

---

### ⚙️ Settings Permissions

Used for tenant-level configuration.

| Permission | Description | Endpoints Using This |
|------------|-------------|---------------------|
| `Settings.Read` | View tenant settings | `GET /tenant/settings` |
| `Settings.Write` | Modify tenant settings | `PATCH /tenant/settings` |

**Typical Role Assignments:**
- **Admin:** `Settings.Read`, `Settings.Write`
- **Manager:** `Settings.Read`, `Settings.Write`
- **Regular User:** `Settings.Read` only (or no access)

**Security Notes:**
- Settings changes affect all tenant users - log all modifications
- Consider requiring re-authentication for sensitive setting changes
- Some settings may need additional permissions (e.g., changing billing info)

---

### 👥 User Management Permissions

Used for managing users within a tenant.

| Permission | Description | Usage |
|------------|-------------|-------|
| `Users.Read` | View users in tenant | List users, view user details, see user activity |
| `Users.Invite` | Invite new users to tenant | Send invitation emails, generate invite links |
| `Users.Manage` | Manage user roles and permissions | Assign/remove roles, enable/disable users |
| `Users.Deactivate` | Deactivate users | Suspend user access (soft delete) |

**Typical Role Assignments:**
- **Admin:** All Users permissions
- **Manager:** `Users.Read`, `Users.Invite`, `Users.Manage`
- **HR:** `Users.Read`, `Users.Invite`
- **Regular User:** No user management permissions

**Security Notes:**
- `Users.Manage` is powerful - user can grant themselves more permissions
- `Users.Deactivate` should not be reversible by the same role (prevents account hijacking)
- Consider separating "view all users" from "view user PII" for privacy

**Status:** *Endpoints not yet implemented - permissions are pre-defined*

---

### 🛡️ Role Management Permissions

Used for managing roles within a tenant.

| Permission | Description | Usage |
|------------|-------------|-------|
| `Roles.Read` | View roles in tenant | List roles, view role permissions, see role assignments |
| `Roles.Create` | Create new roles | Define new roles with specific permission sets |
| `Roles.Update` | Modify existing roles | Change role name, add/remove permissions |
| `Roles.Delete` | Delete roles | Remove roles (if no users assigned) |

**Typical Role Assignments:**
- **Admin:** All Roles permissions
- **Manager:** `Roles.Read` only (view but not modify)
- **Regular User:** No role management permissions

**Security Notes:**
- Role management is tenant-scoped (roles don't cross tenant boundaries)
- `Roles.Update` allows changing permission sets - log all changes
- Prevent deleting roles that are currently assigned to users
- Consider requiring approval workflow for role permission changes

**Status:** *Endpoints not yet implemented - permissions are pre-defined*

---

### 💼 Personal Finance Permissions

Used for Payabo personal finance operations (accounts, transactions, imports, classification, and insights).

| Permission | Description | Usage |
|------------|-------------|-------|
| `PersonalFinance.Accounts.Read` | View personal finance accounts | List and view source accounts (bank accounts, cards, wallets) |
| `PersonalFinance.Accounts.Write` | Create/manage personal finance accounts | Create, update, and archive source accounts |
| `PersonalFinance.Transactions.Read` | View personal finance transactions | Read manual and imported transactions |
| `PersonalFinance.Transactions.Write` | Create/update personal finance transactions | Create manual transactions and apply edits |
| `PersonalFinance.Imports.Create` | Create statement imports | Upload bank/card statement files for ingestion |
| `PersonalFinance.Imports.Read` | View statement imports | View import status, rows, and parsing outcomes |
| `PersonalFinance.Classification.Run` | Run transaction classification | Execute deterministic/AI classification routines |
| `PersonalFinance.Classification.Review` | Review/override classifications | Accept or override low-confidence/pending classifications |
| `PersonalFinance.Insights.Read` | View spending insights | Access summary/category/merchant/account insights |

**Typical Role Assignments:**
- **ConsumerUser:** `PersonalFinance.Accounts.Read`, `PersonalFinance.Accounts.Write`, `PersonalFinance.Transactions.Read`, `PersonalFinance.Transactions.Write`, `PersonalFinance.Imports.Create`, `PersonalFinance.Imports.Read`, `PersonalFinance.Classification.Review`, `PersonalFinance.Insights.Read`
- **Operations/Support:** Read-focused subset (`PersonalFinance.*.Read`) and optional `PersonalFinance.Classification.Review`

**Security Notes:**
- Classification overrides should be auditable (actor, timestamp, prior category, final category).
- Keep import and classification actions scoped to tenant and user-owned accounts.
- For future shared/global rules, require explicit approval before apply.

---

## Permission Categories

### Business Operations Permissions
Permissions for day-to-day business activities:
- Invoice.*
- Payment.*
- Ledger.*

**Who Needs These:** Most users in the system

### Administrative Permissions
Permissions for tenant-level administration:
- Settings.*
- Users.*
- Roles.*

**Who Needs These:** Tenant administrators and managers only

### Platform Administration
Special permissions for AONIK system operators:
- Platform Admin (not a database permission - checked via JWT claims)

**Who Needs This:** AONIK employees only, not tenant users

---

## How Permissions Are Used

### 1. Permission Seeding

Permissions are automatically seeded when the API starts in Development mode.

**File:** `src/Aonik.Platform/Services/Seeding/IdentitySeedService.cs`

**When:** Application startup (Development environment only)

**Idempotent:** Safe to run multiple times - won't create duplicates

### 2. Endpoint Protection

Endpoints specify required permissions using `Policies()`:

```csharp
public override void Configure()
{
    Post("/billing/invoices");
    Policies("Invoice.Create");  // Requires Invoice.Create permission
}
```

**Multiple Permissions (OR logic):**
```csharp
Policies("Invoice.Read", "Invoice.Create");  // User needs either permission
```

**Platform Admin:**
```csharp
Policies("PlatformAdmin");  // Special platform-level access
```

**Policy conventions (role OR permission):**
```csharp
Policies("TenantAdmin"); // Requires TenantAdmin role or Users.Manage permission
Policies("CanOperate");  // Requires Operations role or Payment.Create permission
```

### 3. Permission Checking

When a request arrives:

1. **Authentication** validates JWT token
2. **Authorization** checks if user has required permission:
   - Load user's roles from database
   - Load permissions for those roles
   - Check if required permission is in the list
3. **Allow or Deny** request based on result

**Flow:**
```
User → Role(s) → Permission(s) → Endpoint Access
```

### 4. Database Structure

```sql
-- Permissions table (global)
Permissions
  - Id (Guid)
  - Key (string) - e.g., "Invoice.Create"
  - Description (string)

-- Roles table (tenant-scoped)
Roles
  - Id (Guid)
  - TenantId (Guid)
  - Name (string) - e.g., "Accountant"

-- RolePermissions join table
RolePermissions
  - RoleId (Guid)
  - PermissionId (Guid)

-- UserRoles join table
UserRoles
  - UserId (Guid)
  - RoleId (Guid)
```

---

## Permission Best Practices

### For Developers

**1. Use Specific Permissions**
```csharp
// ✅ Good - specific permission
Policies("Invoice.Create");

// ❌ Bad - too broad
Policies("Invoice.*");  // Wildcard not supported
```

**2. Document Required Permissions**
```csharp
/// <summary>
/// Creates a new invoice.
/// Requires: Invoice.Create permission
/// </summary>
public class CreateInvoiceEndpoint : Endpoint<CreateInvoiceRequest, InvoiceResponse>
{
    public override void Configure()
    {
        Post("/billing/invoices");
        Policies("Invoice.Create");
    }
}
```

**3. Log Permission Denials**
Permission denials are automatically logged with:
- User ID
- Requested permission
- Endpoint
- Timestamp

**4. Test with Minimal Permissions**
When testing endpoints, create test roles with only the minimum required permissions.

### For Administrators

**1. Follow Principle of Least Privilege**
Grant users the minimum permissions needed for their job.

**2. Use Roles, Not Direct Permissions**
Always assign permissions via roles, never directly to users (AONIK doesn't support direct user permissions).

**3. Review Permissions Regularly**
- Audit user roles quarterly
- Remove permissions when job duties change
- Deactivate users who leave the organization

**4. Separate Concerns**
Create separate roles for different job functions:
- ✅ Good: "Accountant" role with Invoice + Ledger permissions
- ❌ Bad: "Employee" role with all permissions

---

## Common Role Examples

### Accountant Role
Typical permissions:
- `Invoice.Create`
- `Invoice.Read`
- `Invoice.Update`
- `Invoice.Issue`
- `Ledger.Read`
- `Ledger.Write`
- `Settings.Read`

**Can:** Manage invoices and ledger entries
**Cannot:** Process payments, manage users, change settings

### Payment Processor Role
Typical permissions:
- `Payment.Create`
- `Payment.Read`
- `Payment.Capture`
- `Payment.Cancel`
- `Invoice.Read` (to see associated invoices)

**Can:** Process payments and captures
**Cannot:** Issue refunds, manage invoices, access ledger

### Manager Role
Typical permissions:
- All Invoice permissions
- All Payment permissions
- All Ledger permissions
- `Settings.Read`
- `Users.Read`
- `Users.Invite`
- `Users.Manage`

**Can:** Perform most business operations and manage team
**Cannot:** Change critical settings, delete roles

### Admin Role
Typical permissions:
- ALL permissions in the system

**Can:** Everything within the tenant
**Cannot:** Manage other tenants (that's Platform Admin)

### Viewer/Auditor Role
Typical permissions:
- `Invoice.Read`
- `Payment.Read`
- `Ledger.Read`
- `Settings.Read`

**Can:** View all data for auditing/reporting
**Cannot:** Modify anything

---

## Frequently Asked Questions

### Q: Can I create custom permissions?

**A:** Not through the UI. Permissions are seeded from code. To add new permissions:

1. Edit `src/Aonik.Platform/Services/Seeding/IdentitySeedService.cs`
2. Add your permission to the array
3. Restart the API (in Development, permissions auto-seed)
4. Create/update roles to include the new permission

### Q: Can permissions span multiple tenants?

**A:** No. While permissions are globally defined, user-role-permission assignments are tenant-scoped. The same user in different tenants has different permissions.

### Q: How do I check permissions in my code (not at endpoint level)?

**A:** Inject `IPermissionService` and call:

```csharp
var hasPermission = await _permissionService.HasPermissionAsync(
    userId, 
    "Invoice.Create", 
    cancellationToken);

if (!hasPermission)
{
    throw new UnauthorizedAccessException("Missing Invoice.Create permission");
}
```

### Q: What's the difference between Azure AD/Auth0 permissions and AONIK permissions?

**A:** 
- **Identity Provider Permissions (Azure/Auth0):** Coarse-grained, control API access (`read:all`, `write:all`)
- **AONIK Database Permissions:** Fine-grained, control specific actions (`Invoice.Create`, `Payment.Read`)

Use IdP permissions to grant API access, AONIK permissions for feature-level control.

### Q: Can I have OR logic for permissions? (User needs A OR B)

**A:** Yes! Use multiple permission arguments:

```csharp
Policies("Invoice.Read", "Invoice.Create");  // User needs either one
```

### Q: Can I have AND logic for permissions? (User needs A AND B)

**A:** Yes, but requires custom authorization handler. The built-in `Policies()` only supports OR logic. For AND logic, create a custom policy:

```csharp
// In authorization setup:
services.AddAuthorization(options =>
{
    options.AddPolicy("InvoiceManager", policy =>
        policy.RequireAssertion(context =>
            context.User.HasClaim("permission", "Invoice.Create") &&
            context.User.HasClaim("permission", "Invoice.Delete")));
});

// In endpoint:
Policies("InvoiceManager");
```

### Q: Are permissions cached?

**A:** Yes, per request. Permissions are loaded once per request and cached in `HttpContext.Items`. If you change a user's roles, they take effect on the next request (no token refresh needed).

---

## See Also

- [Authentication & Authorization Overview](../features/authentication-authorization.md)
- [Managing Roles and Permissions](../guides/roles-and-permissions.md)
- [Azure AD Setup](../guides/authentication-azure-ad.md)
- [Auth0 Setup](../guides/authentication-auth0.md)

---

**Last Updated:** January 9, 2025  
**Total Permissions:** 41
