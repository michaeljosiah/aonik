# Database Configuration Changes

## Summary

Removed InMemory database configuration from Development environment to ensure data persistence and proper migration handling.

## Changes Made

### 1. Infrastructure Configuration (`DependencyInjection.cs`)

**Before:**
- Checked `UseInMemoryDatabase` configuration setting
- Used InMemory database when `UseInMemoryDatabase=true` in Development
- Had three branches: Testing, Development+InMemory, Production+SQL Server

**After:**
- Removed `UseInMemoryDatabase` configuration check
- Always uses SQL Server for non-test environments
- Simplified to single SQL Server configuration path
- Falls back to LocalDB if no connection string in Development

### 2. Program.cs Startup

**Before:**
- Only seeded permissions and catalog data
- No automatic migrations

**After:**
- Added `await dbContext.Database.MigrateAsync()` to run pending migrations automatically
- Logs migration status
- Seeds permissions and catalog data after migration
- Wrapped in try-catch to handle database connectivity issues gracefully

### 3. Configuration Files

**Before (`appsettings.Development.json`):**
```json
"UseInMemoryDatabase": true,
"InMemoryDatabaseName": "AonikDev",
"ConnectionStrings": {
  "DefaultConnection": "Server=127.0.0.1,52286;..."
}
```

**After (`appsettings.Development.json`):**
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=127.0.0.1,52286;..."
}
```

### 4. Test Infrastructure (`CustomWebApplicationFactory.cs`)

**Changed:**
- Explicitly removes SQL Server DbContext registration
- Adds InMemory DbContext specifically for tests
- Uses unique database name per test run: `TestDb_{Guid.NewGuid()}`
- Ensures tests remain isolated from development database

## Impact

### Development Workflow

**Before:**
- Data lost on every API restart
- Bootstrap required after every restart
- No migration history
- Inconsistent with production

**After:**
- Data persists across API restarts
- Bootstrap only required once
- Migrations run automatically on startup
- Consistent with production behavior
- Can inspect database directly using SSMS or Azure Data Studio

### Testing

**No change:**
- Tests continue to use InMemory database
- Each test gets isolated database instance
- Fast test execution
- No database cleanup required

## Next Steps

1. **First-time setup:**
   ```bash
   # Ensure SQL Server is running
   # The API will automatically run migrations on startup
   dotnet run --project src/Aonik.Api
   ```

2. **Access Setup Wizard:**
   - Navigate to https://localhost:5001/setup (or your configured Admin UI)
   - Complete bootstrap to create first tenant and admin user
   - Data will persist after restart

3. **Database Inspection:**
   - Connect to: `Server=127.0.0.1,52286;Database=AonikDb;User ID=sa;Password=!D8X08r.SmzgJMaQnVYSwQ;TrustServerCertificate=true`
   - View tables, data, and migration history using SQL Server Management Studio or Azure Data Studio

## Rollback (if needed)

If you need to temporarily use InMemory database:

```json
// Add to appsettings.Development.json
"UseInMemoryDatabase": true,
"InMemoryDatabaseName": "AonikDev"
```

And revert `DependencyInjection.cs` changes.

However, this is **not recommended** as it defeats the purpose of this change.
