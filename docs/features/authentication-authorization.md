# Authentication & Authorization

AONIK uses a modern authentication and authorization system that separates **who you are** (authentication) from **what you can do** (authorization).

## Overview

### The Big Picture

Think of it like entering a secure building:

1. **Authentication** = Showing your ID badge at the door (proving who you are)
2. **Authorization** = The access card determining which rooms you can enter (what you can do)

In AONIK:
- **External Identity Provider (IdP)** handles authentication (Azure AD or Auth0)
- **AONIK Database** handles authorization (permissions and roles)

### Why This Design?

This separation gives us:
- **Security**: Your identity provider (Microsoft, Google, etc.) handles password security
- **Flexibility**: We can switch identity providers without changing permissions
- **Auditability**: We control exactly what each user can do in our system
- **Multi-tenancy**: Same user can have different permissions in different tenants

---

## How It Works

### Step-by-Step Flow

When you try to access an AONIK endpoint, here's what happens:

#### 1. User Gets a Token
```
User logs in → Identity Provider (Azure/Auth0) → Issues JWT Token
```

The JWT token contains:
- `iss` (issuer): Who issued the token (e.g., `https://login.microsoftonline.com/...`)
- `sub` or `oid` (subject): Unique user ID from the identity provider
- `email`: User's email address
- Custom claims like `aonik_tenant_id`: Which AONIK tenant the user belongs to

#### 2. User Makes Request
```
Frontend → API Request + JWT Token in Authorization Header
```

Example:
```http
GET /billing/invoices/123
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
```

#### 3. AONIK Validates Token
```
API → Validates JWT signature and expiration → Extracts claims
```

The system checks:
- Is the token signed by a trusted identity provider?
- Has the token expired?
- Is the token meant for this API (audience check)?

#### 4. AONIK Resolves Tenant
```
Extract aonik_tenant_id claim → Look up tenant in database → Verify tenant is Active
```

#### 5. AONIK Resolves or Creates User
```
Look up user by (issuer, subject, tenantId) → Create if first login → Store in database
```

**Just-in-Time (JIT) Provisioning**: If this is the user's first login, AONIK automatically creates a user record. However, new users get **zero permissions** by default - someone must explicitly grant them roles.

#### 6. AONIK Checks Permissions
```
Load user's roles → Load roles' permissions → Check if required permission exists
```

Example: To view an invoice, user needs `Invoice.Read` permission.

#### 7. Request Succeeds or Fails
```
Permission granted → Execute endpoint logic → Return response
Permission denied → Return 403 Forbidden
```

---

## Key Concepts

### 1. External Identity (Authentication)

**What is it?**
Your identity in the external system (Microsoft, Google, etc.)

**Key Properties:**
- `ExternalIssuer`: Who manages your identity (e.g., `https://login.microsoftonline.com/`)
- `ExternalSubject`: Your unique ID in that system (e.g., `a1b2c3d4-...`)
- `ExternalTenantId`: (Azure AD only) The Azure tenant you belong to

**Important:** The same real person can have multiple AONIK user accounts if they have different external identities or belong to multiple tenants.

### 2. AONIK User (Internal Record)

**What is it?**
Your user record in AONIK's database.

**Key Properties:**
- `Id`: AONIK's internal user ID (Guid)
- `TenantId`: Which AONIK tenant this user belongs to
- `ExternalIssuer` + `ExternalSubject`: Links to your external identity
- `Status`: Active, Inactive, or Suspended
- `Email`: Your email (optional, for display purposes only)

**Important:** User records are **tenant-scoped**. If you work with multiple tenants, you'll have separate user records in each.

### 3. Permissions (What You Can Do)

**What is it?**
A specific action in the system, like "create an invoice" or "view payments."

**Examples:**
- `Invoice.Create` - Create new invoices
- `Payment.Read` - View payment information
- `Ledger.Write` - Modify ledger accounts

**Key Points:**
- Permissions are **global** - defined once for the whole system
- Permissions are **atomic** - each represents one specific action
- Permissions are **seeded** - automatically created when the app starts

**See:** [Permissions Reference](../reference/permissions.md) for complete list

### 4. Roles (Collections of Permissions)

**What is it?**
A named group of permissions, like "Accountant" or "Manager."

**Examples:**
- **Accountant Role**: Has `Invoice.Read`, `Invoice.Create`, `Ledger.Read`, `Ledger.Write`
- **Manager Role**: Has all Accountant permissions plus `Invoice.Delete`, `Users.Manage`

**Key Points:**
- Roles are **tenant-specific** - each tenant creates their own roles
- Roles are **reusable** - assign the same role to multiple users
- Roles are **manageable** - can be created, updated, or deleted by tenant admins

### 5. Platform Admin (Special Access)

**What is it?**
A special permission level for AONIK system administrators (not tenant users).

**What Can They Do?**
- Create and manage tenants
- Activate/deactivate tenants
- View system-wide health information
- Provision tenant resources

**How Is It Different?**
- Platform admins are identified by **JWT claims**, not database permissions
- They operate **outside tenant scope** (can see all tenants)
- Typically only given to AONIK employees/system operators

**Technical Details:**
- Requires `roles` claim containing `"Aonik.PlatformAdmin"`, OR
- Requires `aonik_platform_admin` scope claim set to `true`

---

## Tenant Routing

AONIK needs to know which tenant you're trying to access. There are three ways to determine this:

### 1. Claim-Based (Production - Recommended)

**How It Works:**
The JWT token contains a custom claim `aonik_tenant_id` with the tenant's GUID.

**Example Token Claim:**
```json
{
  "sub": "user123",
  "email": "john@example.com",
  "aonik_tenant_id": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Configuration:**
```json
{
  "Auth": {
    "TenantRoutingMode": "Claim"
  }
}
```

**When to Use:**
- Production environments
- When identity provider can add custom claims
- Most secure option

### 2. Subdomain-Based (Production - Alternative)

**How It Works:**
The tenant is identified by the subdomain in the URL.

**Example:**
```
https://acme-corp.aonik.io/billing/invoices
         ^^^^^^^
         tenant subdomain
```

The system looks up `acme-corp` in the `Tenant.Subdomain` field.

**Configuration:**
```json
{
  "Auth": {
    "TenantRoutingMode": "Subdomain"
  }
}
```

**Requirements:**
- DNS configured with wildcard records
- `ForwardedHeadersOptions` configured to trust proxy headers
- `AllowedHosts` configured with wildcard subdomain

**When to Use:**
- When you can't add custom claims to JWT
- Multi-tenant SaaS with subdomain URLs
- Requires additional infrastructure setup

### 3. Header-Based (Development Only)

**How It Works:**
Client sends tenant ID in `X-Tenant-Id` header.

**Example:**
```http
GET /billing/invoices
X-Tenant-Id: 550e8400-e29b-41d4-a716-446655440000
```

**Configuration:**
```json
{
  "Auth": {
    "TenantRoutingMode": "Header"
  }
}
```

**Important:**
- **ONLY works in Development environment**
- Automatically disabled in production (security protection)
- Useful for local testing with tools like Postman

---

## Configuration

### Choosing Your Identity Provider

AONIK supports two identity providers. You only use **one** per deployment.

#### Option 1: Azure Active Directory (Entra ID)

**When to Choose:**
- Your organization uses Microsoft 365
- You want enterprise-grade security
- You need integration with Azure services

**Setup Guide:** [Azure AD Setup](../guides/authentication-azure-ad.md)

#### Option 2: Auth0

**When to Choose:**
- You want flexibility with multiple identity sources (Google, GitHub, etc.)
- You need advanced user management features
- You prefer a dedicated authentication service

**Setup Guide:** [Auth0 Setup](../guides/authentication-auth0.md)

### Configuration File Structure

**appsettings.json (Production):**
```json
{
  "Auth": {
    "Provider": "AzureAd",  // or "Auth0"
    "TenantRoutingMode": "Claim",
    
    "AzureAd": {
      "Authority": "https://login.microsoftonline.com/{tenant-id}",
      "Audience": "api://aonik-api",
      "ValidateIssuer": true,
      "ValidateAudience": true,
      "ClockSkew": 300
    }
  },
  
  "PlatformAdmin": {
    "RoleName": "Aonik.PlatformAdmin",
    "ScopeClaimType": "aonik_platform_admin"
  }
}
```

**appsettings.Development.json (Override for local dev):**
```json
{
  "Auth": {
    "TenantRoutingMode": "Header"  // Easier for local testing
  }
}
```

---

## Security Features

### 1. Token Validation

**What We Check:**
- ✅ Token signature (cryptographically signed by trusted IdP)
- ✅ Expiration time (not expired)
- ✅ Issuer (comes from expected identity provider)
- ✅ Audience (intended for AONIK API)
- ✅ Required claims present (sub/oid, email if needed)

**Clock Skew:**
We allow 5 minutes of clock difference between systems to handle time sync issues.

### 2. Tenant Validation

**What We Check:**
- ✅ Tenant exists in database
- ✅ Tenant status is "Active" (not Inactive or Suspended)
- ✅ User is associated with the tenant

**What Happens If Invalid:**
- Request fails with `401 Unauthorized` (missing tenant context)
- Request fails with `403 Forbidden` (inactive tenant)

### 3. User Status Validation

**What We Check:**
- ✅ User exists in database (created via JIT if first login)
- ✅ User status is "Active" (not Inactive or Suspended)

**What Happens If Invalid:**
- Authentication fails during token validation
- User cannot access any endpoints

### 4. Permission Validation

**What We Check:**
- ✅ User has at least one role
- ✅ Role(s) contain the required permission for the endpoint

**What Happens If Invalid:**
- Request fails with `403 Forbidden`
- Error message: "User does not have required permission"

### 5. Production Safeguards

**Header-Based Routing:**
- Automatically disabled in production (even if configured)
- Checks `IHostEnvironment.IsDevelopment()` at runtime

**HTTPS:**
- Required for all environments (enforced in middleware)
- Token metadata validation requires HTTPS

---

## Common Scenarios

### Scenario 1: New User First Login

1. User logs into Azure AD / Auth0 ✅
2. IdP issues JWT token with user's identity ✅
3. User makes first API request to AONIK ✅
4. AONIK validates token ✅
5. AONIK looks up user - **not found** ⚠️
6. AONIK creates new user record (JIT provisioning) ✅
7. New user has **zero permissions** ⚠️
8. Request fails with `403 Forbidden` ❌

**What's Next:**
A tenant administrator must:
1. Log into AONIK admin portal
2. Find the new user
3. Assign role(s) to the user
4. User can now access endpoints based on assigned permissions

### Scenario 2: User Changes Tenant

**Important:** Users are **tenant-scoped**. Switching tenants means switching user accounts.

**Example:**
- John works for Company A (Tenant A) and Company B (Tenant B)
- John has `john@example.com` email
- In Tenant A: John has UserID `123` and role "Accountant"
- In Tenant B: John has UserID `456` and role "Manager"

**How to Switch:**
- Frontend requests new token with different `aonik_tenant_id` claim
- Or user accesses different subdomain (if subdomain routing)
- Backend treats it as completely different user account

### Scenario 3: Platform Admin Creating Tenant

1. Platform admin logs in with special admin credentials ✅
2. IdP issues token with `roles: ["Aonik.PlatformAdmin"]` claim ✅
3. Admin calls `POST /admin/tenants` ✅
4. Endpoint requires `Policies("PlatformAdmin")` ✅
5. System checks for platform admin claim ✅
6. Request succeeds, tenant created ✅

**Key Point:** Platform admin operations **skip tenant validation** because they operate at system level.

### Scenario 4: User Permission Revoked

1. User has role "Accountant" with `Invoice.Read` permission ✅
2. User successfully accesses `GET /billing/invoices/123` ✅
3. Admin removes "Accountant" role from user ⚠️
4. User's next request to same endpoint checks permissions ✅
5. No role found → no permissions → request fails `403 Forbidden` ❌

**Key Point:** Permission changes take effect **immediately** (no token refresh needed).

---

## What's Next?

Now that you understand the authentication and authorization system, dive deeper into specific topics:

- **[Azure AD Setup Guide](../guides/authentication-azure-ad.md)** - Configure Azure Active Directory
- **[Auth0 Setup Guide](../guides/authentication-auth0.md)** - Configure Auth0
- **[Permissions Reference](../reference/permissions.md)** - Complete list of permissions
- **[Managing Roles](../guides/roles-and-permissions.md)** - Create and assign roles
- **[Troubleshooting Auth Issues](../guides/authentication-troubleshooting.md)** - Common problems and solutions

---

## Quick Reference

### HTTP Status Codes

| Code | Meaning | Common Causes |
|------|---------|---------------|
| 200 OK | Success | User has permission, request completed |
| 401 Unauthorized | Not authenticated | No token, invalid token, expired token, inactive user |
| 403 Forbidden | Not authorized | Valid user but missing required permission, inactive tenant |
| 404 Not Found | Resource doesn't exist | Resource not found or user lacks read permission |

### Middleware Order (Critical!)

```
1. app.UseAuthentication()      → Validate JWT, resolve user
2. app.UseAuthorization()       → Check permissions
3. app.UseTenantValidation()    → Validate tenant status
4. app.UseFastEndpoints()       → Route to endpoints
```

**Never change this order!** Each step depends on the previous one.

### Quick Debugging Checklist

- [ ] Is token present in `Authorization: Bearer <token>` header?
- [ ] Is token expired? (Check `exp` claim)
- [ ] Is identity provider configured correctly in appsettings.json?
- [ ] Does token contain `aonik_tenant_id` claim (or subdomain is correct)?
- [ ] Does tenant exist and have status "Active"?
- [ ] Does user exist in database?
- [ ] Does user have status "Active"?
- [ ] Does user have a role assigned?
- [ ] Does that role contain the required permission?

---

**Last Updated:** January 9, 2025  
**Applies to:** AONIK v1.0+
