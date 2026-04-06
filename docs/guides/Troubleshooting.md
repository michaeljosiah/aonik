# Troubleshooting Guide

Common issues and solutions for the AONIK project.

## Table of Contents

- [Build Errors](#build-errors)
- [Test Failures](#test-failures)
- [Runtime Issues](#runtime-issues)
  - [Container App Revision Stuck in Degraded](#error-container-app-revision-stuck-in-degraded--deployment-progress-deadline-exceeded)
  - [Bootstrap Status Returns Disabled](#error-bootstrap-status-returns-disabled-after-deployment)
- [Database Issues](#database-issues)
- [NuGet Package Issues](#nuget-package-issues)

---

## Build Errors

### Error: Package version conflicts

**Symptom:**
```
error NU1605: Detected package downgrade: Microsoft.Extensions.DependencyInjection.Abstractions from 10.0.1 to 9.0.0
```

**Solution:**
Update all packages to use consistent versions. For .NET 10 projects, ensure all Microsoft.Extensions.* packages use version 10.0.1:

```bash
dotnet clean Aonik.sln
dotnet restore Aonik.sln
dotnet build Aonik.sln
```

If issues persist, check `Aonik.Infrastructure.csproj` and ensure:
- `Microsoft.Extensions.DependencyInjection.Abstractions` is at version 10.0.1
- `Microsoft.AspNetCore.Http` (not Http.Abstractions) is at version 2.2.0

---

### Error: 'IServiceCollection' does not contain a definition for 'AddHttpContextAccessor'

**Symptom:**
```
error CS1061: 'IServiceCollection' does not contain a definition for 'AddHttpContextAccessor'
```

**Solution:**
Add the correct NuGet package to `Aonik.Infrastructure.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Http" Version="2.2.0" />
```

NOT:
```xml
<PackageReference Include="Microsoft.AspNetCore.Http.Abstractions" Version="2.2.0" />
```

---

### Error: Entity does not contain a definition for property

**Symptom:**
```
error CS1061: 'Invoice' does not contain a definition for 'InvoiceNumber'
```

**Solution:**
This occurs when EF Core configurations reference properties that don't exist on the entity. 

1. Check the actual entity definition in the owning module (e.g., `src/Aonik.Finance/Entities/Billing/`)
2. Update the corresponding configuration in the module's `Persistence/Configurations/` directory
3. Ensure property names match exactly

Example: If the entity has `CustomerAccountId` but the configuration references `CustomerId`, update the configuration:

```csharp
// Wrong
builder.Property(x => x.CustomerId).IsRequired();

// Correct
builder.Property(x => x.CustomerAccountId).IsRequired();
```

---

### Error: Constructor parameter mismatch

**Symptom:**
```
error CS7036: There is no argument given that corresponds to the required parameter 'tenantProvider' of 'BillingService'
```

**Solution:**
Services require an `ITenantProvider` parameter. In tests, create a mock:

```csharp
private class TestTenantProvider : ITenantProvider
{
    private readonly Guid _tenantId;
    public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;
    public Guid GetCurrentTenantId() => _tenantId;
    public bool TryGetCurrentTenantId(out Guid tenantId)
    {
        tenantId = _tenantId;
        return true;
    }
}

// Usage
var tenantProvider = new TestTenantProvider(Guid.NewGuid());
var service = new BillingService(context, tenantProvider);
```

---

## Test Failures

### Test: Expected property value mismatch

**Symptom:**
```
Expected result.InvoiceNumber to be "INV-001", but "" has a length of 0
```

**Cause:**
The service is not populating all fields correctly, often because:
1. The entity property doesn't exist
2. The service maps to a DTO but doesn't fill all fields
3. The entity-to-DTO mapping is incomplete

**Solution:**
Check the service implementation. If the entity doesn't have the property the test expects, either:
1. Update the entity to include it
2. Update the test to check properties that actually exist
3. Fix the service to populate the DTO correctly

---

### Test: Entity method does not exist

**Symptom:**
```
error CS1061: 'PaymentIntent' does not contain a definition for 'Authorize'
```

**Cause:**
The codebase uses **anemic domain entities** - entities are data containers without behavior methods.

**Solution:**
Manipulate properties directly instead of calling methods:

```csharp
// ❌ Don't do this
payment.Authorize();

// ✅ Do this instead
payment.Status = "Authorized";
await context.SaveChangesAsync();
```

---

### Test: Tests fail when run together but pass individually

**Cause:**
Shared database state between tests.

**Solution:**
Ensure each test uses a unique database name:

```csharp
var options = new DbContextOptionsBuilder<AonikDbContext>()
    .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}") // Unique per test
    .Options;
```

---

## Runtime Issues

### Error: Container App revision stuck in "Degraded" / "Deployment Progress Deadline Exceeded"

**Symptom:**
New ACA revision never becomes healthy. `az containerapp revision list` shows the revision as `Unhealthy`/`Degraded` with `ProvisioningState: Failed`. Startup probe reports thousands of failures with "connection refused". The old revision continues serving traffic as a fallback.

**Cause:**
An `IHostedService` (such as `AgentConfigurationSeedingService`) blocks `StartAsync` because it cannot connect to the database. EF Core's `SqlServerRetryingExecutionStrategy` retries internally with exponential backoff (3s, 7s, 15s...) before throwing, which blocks the host from ever starting the Kestrel web server. In .NET, all hosted services must complete `StartAsync` before Kestrel begins listening.

**Common root cause — SQL FQDN double-dot:**
`environment().suffixes.sqlServerHostname` returns `.database.windows.net` (with leading dot). If the Bicep template constructs the FQDN as `'${sqlServer.name}.${sqlServerHostnameSuffix}'`, the result has a double dot: `aonik-dev-sql..database.windows.net`. This causes DNS resolution failures.

**Fix:**
1. Check `iac/azure/modules/data.bicep` — the FQDN construction must not add an extra dot:
   ```bicep
   // Wrong — double dot
   var sqlFqdn = '${sqlServer.name}.${sqlServerHostnameSuffix}'

   // Correct — no extra dot
   var sqlFqdn = '${sqlServer.name}${sqlServerHostnameSuffix}'
   ```
2. Verify the Key Vault connection string secret has the correct hostname:
   ```bash
   az keyvault secret show --vault-name <vault> --name "ConnectionStrings--DefaultConnection" --query value -o tsv
   ```
3. Redeploy after fixing.

**Diagnosis commands:**
```bash
# List revisions and health
az containerapp revision list -n aonik-dev-api -g rg-aonik-dev-uksouth -o table

# Check container logs for SQL errors
az containerapp logs show -n aonik-dev-api -g rg-aonik-dev-uksouth --type console --follow
```

---

### Error: Bootstrap status returns "disabled" after deployment

**Symptom:**
`GET /bootstrap/status` returns:
```json
{
  "state": "disabled",
  "bootstrapEnabled": false,
  "message": "Bootstrap is disabled. Enable Bootstrap:Enabled to perform first-run setup."
}
```
The setup wizard shows bootstrap availability but the "Run bootstrap" button never enables.

**Cause:**
The `Bootstrap__Enabled` environment variable is missing from the API container. The `BootstrapOptions.Enabled` property defaults to `false`. Having only `Bootstrap__SetupSecret` is not sufficient — `Bootstrap__Enabled=true` must also be set.

**Fix:**
Ensure `iac/azure/stacks/aca/main.bicep` includes both env vars in the conditional block:
```bicep
empty(bootstrapSetupSecret) ? [] : [
  {
    name: 'Bootstrap__Enabled'
    value: 'true'
  }
  {
    name: 'Bootstrap__SetupSecret'
    secretRef: 'bootstrap-setup-secret'
  }
]
```

**Verification:**
```bash
# Check env vars on the container
az containerapp show -n aonik-dev-api -g rg-aonik-dev-uksouth \
  --query "properties.template.containers[0].env" -o json

# Verify API response
curl -s https://<api-url>/bootstrap/status | jq .
```

---

### Error: No tenant context available

**Symptom:**
API endpoints return 400 Bad Request with tenant-related errors.

**Cause:**
The `ITenantProvider` depends on HTTP context, which might not be properly configured in tests.

**Solution:**
For API tests, ensure the `CustomWebApplicationFactory` properly configures tenant headers:

```csharp
var request = new HttpRequestMessage(HttpMethod.Get, "/api/invoices");
request.Headers.Add("X-Tenant-Id", tenantId.ToString());
var response = await client.SendAsync(request);
```

---

### Error: Database migration fails

**Symptom:**
```
Unable to create an object of type 'AonikDbContext'
```

**Solution:**
Prefer the migrator for first-install migration + seed:

```bash
dotnet run --project src/Aonik.Migrator
```

If you need direct EF commands, specify both project and startup project:

```bash
dotnet ef migrations add MigrationName \
  --project src/Aonik.Infrastructure \
  --startup-project src/Aonik.Api

dotnet ef database update \
  --project src/Aonik.Infrastructure \
  --startup-project src/Aonik.Api

# Then apply platform module migrations
dotnet ef database update \
  --project src/Aonik.Platform \
  --startup-project src/Aonik.Api \
  --context PlatformDbContext
```

---

## Database Issues

### LocalDB not found

**Symptom:**
```
A network-related or instance-specific error occurred while establishing a connection to SQL Server
```

**Solution:**

1. Check if LocalDB is installed:
```bash
sqllocaldb info
```

2. If not installed, install SQL Server Express with LocalDB:
   - Download from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
   - Select "Express" edition
   - Choose "Download Media" and select "LocalDB"

3. Alternatively, use InMemory database for development:

In `appsettings.Development.json`:
```json
{
  "UseInMemoryDatabase": "true",
  "InMemoryDatabaseName": "AonikDevDb"
}
```

---

### Error: Database already exists

**Symptom:**
Tests fail because database wasn't cleaned up.

**Solution:**
Always use unique database names in tests:

```csharp
.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
```

For SQL Server integration tests, clean up in test teardown:

```csharp
public void Dispose()
{
    context.Database.EnsureDeleted();
    context.Dispose();
}
```

---

## NuGet Package Issues

### Package restore fails

**Solution:**
```bash
# Clear NuGet caches
dotnet nuget locals all --clear

# Restore packages
dotnet restore Aonik.sln

# Rebuild
dotnet build Aonik.sln
```

---

### Transitive package version conflicts

**Symptom:**
Multiple versions of the same package are being referenced.

**Solution:**
Add explicit package references to force a specific version:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.1" />
</ItemGroup>
```

Or use a `Directory.Packages.props` file for centralized package management (recommended for larger projects).

---

## Getting Help

If you encounter an issue not covered here:

1. Check the [CHANGELOG.md](../CHANGELOG.md) for recent changes
2. Review the [AGENTS.md](../AGENTS.md) coding guidelines
3. Check the [Testing.md](Testing.md) for test-specific issues
4. Search existing GitHub issues
5. Create a new GitHub issue with:
   - Full error message
   - Steps to reproduce
   - Your environment (.NET version, OS, etc.)
   - Relevant code snippets

---

## Quick Fixes Checklist

When encountering build or test issues, try these steps in order:

```bash
# 1. Clean and restore
dotnet clean Aonik.sln
dotnet restore Aonik.sln

# 2. Clear NuGet cache (if restore issues)
dotnet nuget locals all --clear
dotnet restore Aonik.sln

# 3. Build
dotnet build Aonik.sln

# 4. Run tests
dotnet test Aonik.sln

# 5. If database issues, try InMemory
# Edit appsettings.Development.json:
# "UseInMemoryDatabase": "true"
```

---

## Common Environment Issues

### .NET SDK Version

Ensure you have .NET 10 SDK installed:

```bash
dotnet --list-sdks
```

Should show version 10.x.x. If not, download from:
https://dotnet.microsoft.com/download/dotnet/10.0

---

### IDE-Specific Issues

**Visual Studio:**
- Clean Solution: Build → Clean Solution
- Rebuild Solution: Build → Rebuild Solution
- Close and reopen the solution if IntelliSense is stale

**VS Code:**
- Reload window: Ctrl+Shift+P → "Developer: Reload Window"
- Restart OmniSharp: Ctrl+Shift+P → "OmniSharp: Restart OmniSharp"

**Rider:**
- Invalidate Caches: File → Invalidate Caches / Restart
- Rebuild Solution: Build → Rebuild All

---

## Performance Issues

### Slow builds

**Solution:**
```bash
# Use parallel builds
dotnet build Aonik.sln --parallel

# Skip tests during build
dotnet build Aonik.sln --no-restore

# Build specific project
dotnet build src/Aonik.Api
```

### Slow tests

**Solution:**
- Use InMemory database (faster than SQL Server)
- Run specific test categories: `dotnet test --filter "Category=Unit"`
- Run tests in parallel: `dotnet test --parallel`
