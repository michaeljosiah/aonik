---
title: Quickstart
description: Get a local Aonik stack running and bootstrap your first tenant in about 15 minutes.
sidebar_label: Quickstart
sidebar_position: 2
---

# Get Aonik running locally in 5 steps

:::info
Stand up the API, Worker, Admin UI, Payabo web, and a Qdrant container, then bootstrap your first tenant and platform admin.
:::

## Why this matters

This is the shortest path from a fresh clone to a working multi-service Aonik stack with a tenant and an owner user. Use it for first-run validation, demos, and clean local environments. Production deployment is covered separately in the [Operations](../operations/index.md) section.

## Before you start

You need:

- **.NET 10 SDK** (10.0.100). Check with `dotnet --list-sdks`.
- **Docker** running. Aspire pulls a Qdrant container at startup.
- **SQL Server LocalDB** (Windows). Ships with SQL Server Express. Verify with `sqllocaldb info`.
- **Node.js 18+** for the Admin UI and Payabo web Vite dev servers.
- **Git** and a modern shell (the commands below use PowerShell).

Aonik's local default connection string targets `(localdb)\MSSQLLocalDB`. On macOS or Linux, see [Install & Configure → Connect to SQL Server](../install-configure/index.md) for an alternative.

## Steps

### 1. Clone the repo

```powershell
git clone https://github.com/michaeljosiah/aonik.git
cd aonik
```

No `npm install` is required ahead of time — Aspire runs the Vite dev servers under the hood and they install their own dependencies on first launch.

### 2. Configure the bootstrap secret

The `/bootstrap` endpoint is disabled by default. Enable it and set a one-time setup secret using [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) scoped to the API project:

```powershell
dotnet user-secrets --project src/Aonik.Api set Bootstrap:Enabled true
$secret = [guid]::NewGuid().ToString()
dotnet user-secrets --project src/Aonik.Api set Bootstrap:SetupSecret $secret
Write-Host "Bootstrap secret: $secret"
```

Copy the secret somewhere temporary. You will pass it to `/bootstrap` in step 4. You can retrieve it later with `dotnet user-secrets --project src/Aonik.Api list`.

### 3. Start the Aspire orchestrator

```powershell
dotnet run --project src/Aonik.AppHost
```

Aspire launches the API, Worker, Admin UI, Payabo web, and a Qdrant container. The Aspire dashboard opens automatically and lists every resource with its URL.

Default URLs:

| Resource          | URL                          |
| ----------------- | ---------------------------- |
| Aspire dashboard  | https://localhost:21183      |
| API               | https://localhost:5001       |
| Scalar API ref    | https://localhost:5001/scalar |
| Admin UI          | http://localhost:5173        |
| Payabo web        | http://localhost:5174        |
| Qdrant REST       | http://localhost:6333        |

Wait for every resource in the dashboard to report `Running` before continuing.

### 4. Bootstrap your first tenant + admin

In a second terminal, call the bootstrap endpoint with the secret from step 2. The endpoint is anonymous but rejects any value that doesn't match `Bootstrap:SetupSecret`.

```powershell
$body = @{
  setupSecret      = '<paste-the-secret-from-step-2>'
  ownerEmail       = 'you@example.com'
  ownerDisplayName = 'Platform Admin'
} | ConvertTo-Json

curl.exe --insecure https://localhost:5001/bootstrap `
  -H 'Content-Type: application/json' `
  -d $body
```

A successful response looks like this:

```json
{
  "tenantId": "00000000-0000-0000-0000-000000000000",
  "tenantName": "Default",
  "tenantCreated": true,
  "userId": "00000000-0000-0000-0000-000000000000",
  "userCreated": true,
  "platformAdminAssigned": true,
  "tenantAdminAssigned": true,
  "ownerEmail": "you@example.com",
  "success": true
}
```

Calling `/bootstrap` again returns **409 Conflict**. For additional tenants, use the regular tenant administration endpoints in the [API Reference](/api/aonik-api).

### 5. Verify the stack is healthy

```powershell
curl.exe --insecure https://localhost:5001/health
```

Expect `200 OK` with `{ "Status": "Healthy", ... }`. You can also:

- Open https://localhost:5001/scalar to browse the API
- Open http://localhost:5173 for the Admin UI
- Open http://localhost:5174 for the Payabo web shell

Until you wire a real identity provider in [Identity & Access](../identity-access/index.md), the Admin UI and Payabo logins will not succeed — the platform is running, but no IdP can validate the tokens.

## Troubleshooting

### LocalDB connection failure on startup

**Symptom.** The API logs `Resolved Aonik SQL connection: server=(localdb)\MSSQLLocalDB` followed by connection failures, or migrations report `Skipping database initialization due to connectivity issues.`

**Cause.** SQL Server LocalDB is not installed on the host.

**Fix.** Install **SQL Server Express** (which bundles LocalDB) or **SQL Server Developer Edition**, then run `sqllocaldb start MSSQLLocalDB`.

### Qdrant container fails to start

**Symptom.** The Aspire dashboard shows Qdrant as failed; the API logs `Qdrant health check timed out` or `Qdrant /readyz reported not-ready`.

**Cause.** Docker is not running, or the local port `6333` / `6334` is taken.

**Fix.** Start Docker Desktop, then re-run `dotnet run --project src/Aonik.AppHost`. If a port is in use, stop the conflicting container with `docker ps` + `docker stop`.

### Bootstrap returns `503 Service Unavailable`

**Symptom.** Response body: `Bootstrap is enabled but Bootstrap:SetupSecret is not configured.`

**Cause.** You set `Bootstrap:Enabled` but not `Bootstrap:SetupSecret`.

**Fix.** Run `dotnet user-secrets --project src/Aonik.Api set Bootstrap:SetupSecret <value>` and restart the API resource from the Aspire dashboard.

### Bootstrap returns `403 Forbidden`

**Symptom.** Response body: `The provided install code is invalid.`

**Cause.** The `setupSecret` in your request does not exactly match `Bootstrap:SetupSecret`. Comparison is constant-time and case-sensitive.

**Fix.** Re-fetch the secret with `dotnet user-secrets --project src/Aonik.Api list` and retry.

### Bootstrap returns `409 Conflict`

**Symptom.** `Bootstrap has already completed. Use the tenant administration endpoints for additional tenant setup.`

**Cause.** A tenant already exists. Bootstrap is one-time.

**Fix.** Use the tenant administration endpoints in the [API Reference](/api/aonik-api) to provision additional tenants.

## What's next

- **Make logins work.** Wire a real identity provider in [Identity & Access](../identity-access/index.md).
- **Configure your first product.** [Configure Payabo](../products/payabo/configure.md) walks through the tenant settings Payabo needs.
- **Understand what you just started.** [Architecture at a glance](architecture-at-a-glance.md) explains the moving parts of the stack.
