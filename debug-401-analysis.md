# 401 Authorization Issue - Root Cause Analysis

## Summary

Your 401 error when calling the UserInfo endpoint is likely caused by **email not being stored in the database**, making it impossible to query users by email. This happens when your JWT token doesn't contain the expected email claims.

## Root Cause

### How Email Storage Works

1. **During Authentication** (AonikAuthenticationSetup.cs:163-164):
   ```csharp
   var email = claims.FirstOrDefault(c => c.Type == "email")?.Value
               ?? claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
   ```

2. **Email is passed to** `UserIdentityService.ResolveOrCreateUserAsync` (line 217)

3. **Email is stored** in the User entity (UserIdentityService.cs:124):
   ```csharp
   Email = email,  // Nullable - only if present
   ```

### The Problem

If your JWT token **doesn't contain** either:
- `email` claim, OR
- `preferred_username` claim

Then `email` will be `null`, and the user record is created/updated with `Email = NULL`.

### Why Email Is Nullable

From the design documentation:
> `Email`: Your email (optional, **for display purposes only**)

**The system uses `ExternalIssuer` + `ExternalSubject` as the true identity**, not email.

Email is nullable because:
- Not all IdPs guarantee an email claim
- Email might not be verified at the IdP
- The system must work with minimal claims (`iss` + `sub`/`oid` only)

## Diagnostic Steps

### Step 1: Check Your JWT Token Claims

Decode your JWT token (use jwt.io or jwt.ms) and look for these claims:

```json
{
  "iss": "https://...",           // ✅ Required - ExternalIssuer
  "sub": "a1b2c3...",              // ✅ Required - ExternalSubject (or "oid")
  "email": "michael.josiah@...",  // ❓ Is this present?
  "preferred_username": "...",     // ❓ Or this?
  "upn": "...",                    // ❓ Or this?
  "tid": "...",                    // Optional - ExternalTenantId (Azure only)
  "aonik_tenant_id": "..."         // Tenant routing
}
```

### Step 2: Check the Email Claim Priority

The system checks for email in this order (ClaimsEmailResolver.cs):
1. `email`
2. `preferred_username`
3. `upn`
4. `https://aonik.app/email` (custom claim)
5. `ClaimTypes.Email` (.NET standard)
6. `ClaimTypes.Upn` (.NET standard)
7. `ClaimTypes.Name` (only if contains '@')

### Step 3: Run Updated SQL Queries

Use the updated `debug-user-roles.sql` file:

1. **Query #1**: Try to find by email (might return nothing)
2. **Query #1b**: List ALL users to see what's actually in the database
3. **Query #1c**: Find by ExternalSubject from your JWT token

### Step 4: Check Database Records

Look at the actual User records in the database:

```sql
-- See all users and their emails
SELECT 
    u.Id,
    u.Email,  -- This might be NULL!
    u.ExternalIssuer,
    u.ExternalSubject,
    u.Status,
    u.CreatedAt
FROM Users u
ORDER BY u.CreatedAt DESC;
```

## Solutions

### Option 1: Fix Your Identity Provider (Recommended)

**For Azure AD / Entra ID:**
- Ensure `email` claim is included in token
- Configure token configuration in Azure AD app registration:
  - Add optional claim: `email`
  - Or use `preferred_username` (usually includes email)

**For Auth0:**
- Add email to JWT in Rules or Actions
- Example Auth0 Rule:
  ```javascript
  function(user, context, callback) {
    context.accessToken['email'] = user.email;
    callback(null, user, context);
  }
  ```

### Option 2: Add Custom Email Claim

If you can't modify standard claims, add a custom claim (system already supports this):

```json
{
  "https://aonik.app/email": "michael.josiah@mailinator.com"
}
```

This is already in the ClaimsEmailResolver priority list!

### Option 3: Update Existing User Records (Temporary Fix)

If users were already created without emails, manually update them:

```sql
-- Update user email manually (use ExternalSubject to identify)
UPDATE Users
SET Email = 'michael.josiah@mailinator.com',
    UpdatedAt = GETUTCDATE()
WHERE ExternalSubject = 'YOUR-ACTUAL-SUBJECT-FROM-JWT';
```

### Option 4: Query by ExternalSubject Instead of Email

If your admin UI queries by email, update it to query by ExternalSubject or both:

```csharp
// Instead of:
var user = await _dbContext.Users
    .FirstOrDefaultAsync(u => u.Email == email);

// Use:
var user = await _dbContext.Users
    .FirstOrDefaultAsync(u => 
        u.ExternalSubject == subject || 
        u.Email == email);
```

## Next Steps

1. **Decode your JWT token** - Check what claims are actually present
2. **Run Query #1b** from debug-user-roles.sql - See what's in the database
3. **Compare** - Are users being created with Email = NULL?
4. **Fix at source** - Configure your IdP to include email claims

## Additional Debugging

### Check Application Logs

Look for log entries during authentication (AonikAuthenticationSetup.cs:245):

```
Authenticated user {UserId} in tenant {TenantId} (Status: {Status})
```

### Enable Detailed Logging

In appsettings.Development.json:

```json
{
  "Logging": {
    "LogLevel": {
      "Aonik.Infrastructure.Authentication": "Debug",
      "Microsoft.AspNetCore.Authentication": "Debug"
    }
  }
}
```

This will show JWT claim extraction and user resolution details.

## Why The Design Is Correct

**The current design is intentionally flexible:**

1. **Identity Provider Independence**: Works with any IdP, even if email isn't available
2. **Privacy**: Email isn't required for internal user identification
3. **Reliability**: External identity (`iss` + `sub`) is guaranteed by JWT spec
4. **Security**: Email can change; external subject cannot

**Email is "nice to have" for display, but not required for authentication.**

## Conclusion

**Your 401 error root cause is likely**:
- JWT doesn't contain email claim
- User record has `Email = NULL`
- Query by email returns no results
- System treats you as an unknown user or fails authorization

**Solution**: Configure your IdP to include email in JWT claims, or use ExternalSubject for user lookups instead of email.
