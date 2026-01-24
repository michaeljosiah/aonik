# Auth0 Email Claim Configuration Guide

## What Changed

✅ **Fixed**: Updated `AonikAuthenticationSetup.cs` to use `ClaimsEmailResolver.GetEmail()` during token validation.

**Before** (line 163-164):
```csharp
var email = claims.FirstOrDefault(c => c.Type == "email")?.Value
            ?? claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
```

**After** (line 163):
```csharp
var email = ClaimsEmailResolver.GetEmail(context.Principal);
```

This means the system now checks **7 different email claim types** (in priority order):

1. `email` ✅ **Standard claim - Works with Auth0**
2. `preferred_username`
3. `upn`
4. `https://aonik.app/email` ✅ **Custom namespaced claim - Recommended for Auth0**
5. `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress` (ClaimTypes.Email)
6. `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn` (ClaimTypes.Upn)
7. `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name` (ClaimTypes.Name - only if contains '@')

## Auth0 Configuration Options

You have **two options** for adding email to your Auth0 tokens:

### Option 1: Standard `email` Claim (Simplest)

**Use this if**: You want the simplest setup

**Auth0 Action Code**:
```javascript
exports.onExecutePostLogin = async (event, api) => {
  if (event.user.email) {
    api.accessToken.setCustomClaim('email', event.user.email);
  }
};
```

**Resulting Token**:
```json
{
  "iss": "https://YOUR-DOMAIN.auth0.com/",
  "sub": "auth0|123456789",
  "email": "michael.josiah@mailinator.com",
  "aud": "YOUR-API-IDENTIFIER"
}
```

### Option 2: Namespaced Custom Claim (Recommended)

**Use this if**: You want to follow Auth0 best practices for custom claims

**Auth0 Action Code**:
```javascript
exports.onExecutePostLogin = async (event, api) => {
  if (event.user.email) {
    // Option A: Use Aonik's namespace
    api.accessToken.setCustomClaim('https://aonik.app/email', event.user.email);
    
    // Option B: Use your own namespace (also works, but requires code change)
    // api.accessToken.setCustomClaim('https://yourdomain.com/email', event.user.email);
  }
};
```

**Resulting Token**:
```json
{
  "iss": "https://YOUR-DOMAIN.auth0.com/",
  "sub": "auth0|123456789",
  "https://aonik.app/email": "michael.josiah@mailinator.com",
  "aud": "YOUR-API-IDENTIFIER"
}
```

**Why namespaced?** Auth0 best practices recommend namespacing custom claims to avoid collisions with standard OIDC claims.

## Step-by-Step Auth0 Setup

### 1. Create an Auth0 Action

1. Log into **Auth0 Dashboard**
2. Go to **Actions** → **Flows** → **Login**
3. Click **+ Custom** button
4. Name it: `Add Email to Access Token`
5. Paste the code (choose Option 1 or 2 above)
6. Click **Deploy**

### 2. Add Action to Login Flow

1. In the Login Flow, drag your new action into the flow
2. Place it **after** the start node
3. Click **Apply**

### 3. Test the Token

#### Using Auth0 Debug Extension (Easy)

1. Go to **Authentication** → **Authentication API** → **API Explorer**
2. Get a test access token
3. Decode it at https://jwt.io
4. Verify `email` or `https://aonik.app/email` claim is present

#### Using curl (Advanced)

```bash
# Get token
curl --request POST \
  --url 'https://YOUR-DOMAIN.auth0.com/oauth/token' \
  --header 'content-type: application/json' \
  --data '{
    "client_id": "YOUR_CLIENT_ID",
    "client_secret": "YOUR_CLIENT_SECRET",
    "audience": "YOUR_API_IDENTIFIER",
    "grant_type": "client_credentials"
  }'

# Decode and check claims
# Copy the access_token and paste at https://jwt.io
```

### 4. Verify in AONIK

After deploying the Auth0 Action:

1. **Clear any cached tokens** in your client app
2. **Login again** to get a new token with the email claim
3. **Make an API request** to AONIK
4. **Check application logs** for:
   ```
   Authenticated user {UserId} in tenant {TenantId} (Status: Active)
   ```
5. **Query the database**:
   ```sql
   SELECT Id, Email, ExternalSubject, Status
   FROM Users
   WHERE Email = 'michael.josiah@mailinator.com';
   ```

## Troubleshooting

### Email Still NULL After Auth0 Update?

**Issue**: Token has email claim, but User.Email is still NULL

**Causes**:
1. ❌ Old cached token (doesn't have email claim yet)
2. ❌ User was created before email claim was added
3. ❌ Auth0 Action not properly deployed/applied to flow

**Solutions**:
```bash
# Solution 1: Force token refresh in your client
# Delete tokens and re-login

# Solution 2: Check Auth0 Action is in the flow
# Auth0 Dashboard → Actions → Flows → Login → Verify action is present

# Solution 3: Check Auth0 Action logs
# Auth0 Dashboard → Monitoring → Logs → Look for action execution
```

### Verify Email is Being Extracted

Add temporary logging to see what email is being extracted:

In `AonikAuthenticationSetup.cs` after line 163, add:
```csharp
var email = ClaimsEmailResolver.GetEmail(context.Principal);
logger.LogInformation("Extracted email claim: {Email}", email ?? "<null>");
```

Check application logs after authentication.

### Database Update for Existing Users

If users were already created without email, they'll be updated on next login (UserIdentityService.cs:50-55):

```csharp
if (!string.IsNullOrEmpty(email) && existingUser.Email != email)
{
    existingUser.Email = email;
    await _dbContext.SaveChangesAsync(ct);
    _logger.LogInformation("Updated email for user {UserId}", existingUser.Id);
}
```

**Or manually update**:
```sql
UPDATE Users
SET Email = 'michael.josiah@mailinator.com',
    UpdatedAt = GETUTCDATE()
WHERE ExternalSubject = 'auth0|123456789';  -- Your actual Auth0 sub
```

## What Claims AONIK Expects from Auth0

| Claim | Required? | Purpose | Auth0 Default |
|-------|-----------|---------|---------------|
| `iss` | ✅ Yes | Issuer identifier | ✅ Always included |
| `sub` | ✅ Yes | User unique ID | ✅ Always included |
| `aud` | ✅ Yes | API identifier | ✅ Always included |
| `exp` | ✅ Yes | Token expiration | ✅ Always included |
| `email` | ⚠️ Optional | User's email | ❌ Requires Action |
| `aonik_tenant_id` | ⚠️ Optional* | Tenant routing | ❌ Requires Action |

*Required if using Claim-based tenant routing mode

## Example Full Auth0 Action (Production)

```javascript
/**
 * Handler that will be called during the execution of a PostLogin flow.
 *
 * @param {Event} event - Details about the user and the context in which they are logging in.
 * @param {PostLoginAPI} api - Interface whose methods can be used to change the behavior of the login.
 */
exports.onExecutePostLogin = async (event, api) => {
  const namespace = 'https://aonik.app';
  
  // Add email to access token
  if (event.user.email) {
    api.accessToken.setCustomClaim(`${namespace}/email`, event.user.email);
  }
  
  // Add tenant ID to access token (if using claim-based routing)
  // You'll need to store this in user_metadata or app_metadata
  if (event.user.app_metadata && event.user.app_metadata.aonik_tenant_id) {
    api.accessToken.setCustomClaim(`${namespace}/tenant_id`, event.user.app_metadata.aonik_tenant_id);
  }
  
  // Optional: Add user roles (if using Auth0 RBAC)
  if (event.authorization && event.authorization.roles) {
    api.accessToken.setCustomClaim(`${namespace}/roles`, event.authorization.roles);
  }
};
```

## Verification Checklist

After configuration, verify:

- [ ] Auth0 Action is created and deployed
- [ ] Auth0 Action is added to Login Flow
- [ ] Test token contains email claim (check at jwt.io)
- [ ] Old tokens are cleared/refreshed in client
- [ ] Login to AONIK and make API request
- [ ] Check AONIK logs for successful authentication
- [ ] Query database to confirm Email is populated
- [ ] UserInfo endpoint returns expected data

## Next Steps

1. ✅ Code is now fixed to use `ClaimsEmailResolver`
2. 🔧 Configure Auth0 using Option 1 or 2 above
3. 🧪 Test with a new login
4. 📊 Verify email is stored in database
5. ✅ 401 error should be resolved

## Additional Notes

### Why This Works for Both Providers

The `ClaimsEmailResolver` checks 7 different claim types, which covers:

- **Auth0**: Uses `email` or custom namespaced claims
- **Azure AD / Entra ID**: Uses `preferred_username`, `upn`, or `email`
- **Other OIDC providers**: Uses standard `email` claim

This makes AONIK flexible and provider-agnostic!

### Performance Impact

None. The email resolver iterates through claim types in order and returns on first match. With email being the first checked type, it's typically O(1) or O(2) operation.
