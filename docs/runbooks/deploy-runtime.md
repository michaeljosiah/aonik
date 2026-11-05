# Runbook: Deploy Runtime

Workflow: `.github/workflows/cd-deploy.yml`

## Purpose
Deploy ACA runtime updates using one explicit image release version.

## Inputs
- `environment`: `dev|staging|prod`
- `resource_group` (optional override)
- `workload_name` (optional override)
- `acr_login_server` (optional override)
- `image_version` (optional; defaults to `github.sha`)
- `use_digest_references` (`true` recommended)
- `location` (optional)
- `mode`: `what-if` or `deploy`
- `skip_image_validation` (advanced/emergency only)

## Required GitHub Environment Secrets/Vars
- Required repo vars: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- Required secrets: `SQL_ADMIN_PASSWORD`, `BOOTSTRAP_SETUP_SECRET`
- Optional secrets: `ACS_CONNECTION_STRING`, `VERIFICATION_HASH_KEY`, `AZURE_CLIENT_SECRET`
- Required variable: `AZURE_RESOURCE_GROUP`
- Optional variable: `WORKLOAD_NAME`

### Bootstrap install code

The runtime deploy workflow passes `BOOTSTRAP_SETUP_SECRET` directly into the API Container App as `Bootstrap__SetupSecret`.

- Store it as a GitHub Environment secret per environment.
- If the secret is omitted, the API falls back to image defaults or any value already present on the host platform.
- When the secret is configured, the Bicep template also sets `Bootstrap__Enabled=true` automatically. Both env vars are required for bootstrap to function — `Bootstrap__SetupSecret` alone is not sufficient.

### Runtime app settings

All runtime app settings are defined as **individual** GitHub Environment variables or secrets. The deploy workflow collects any env var whose name starts with a recognised prefix and forwards it to the API or Worker container.

Recognised API prefixes: `AI__`, `SETTINGS__`, `FINANCE__`
Recognised Worker prefixes: `WORKER__`

Use `.NET` double-underscore convention for nested keys (e.g. `Finance:PersonalFinance:Plaid:ClientId` → `FINANCE__PERSONALFINANCE__PLAID__CLIENTID`).

Store credentials as **Secrets**; non-sensitive values as **Variables**.

| Category | Variable (var) | Secret |
|----------|---------------|--------|
| AI | `AI__PROVIDER`, `AI__OPENAI__MODEL` | `AI__OPENAI__APIKEY` |
| AI / CodeAct sub-agent sandbox (Spec 025) | `AI__CODEACT__PROVIDER` (`AcaSessions` to enable; `Hyperlight` for local Linux /dev/kvm hosts; anything else falls back to the tool-loop path) | `AI__CODEACT__NONCESIGNINGKEY` (32-byte hex or base64; required only when `Provider=AcaSessions`) |
| Auth | `SETTINGS__AUTH_PROVIDER`, `SETTINGS__AUTH_AUTH0_DOMAIN`, `SETTINGS__AUTH_AUTH0_AUDIENCE`, `SETTINGS__AUTH_AUTH0_CLIENTID`, `SETTINGS__AUTH_AUTH0_CONNECTION`, `SETTINGS__AUTH_AUTH0_MANAGEMENTCLIENTID`, `SETTINGS__AUTH_AUTH0_MANAGEMENTAUDIENCE` | `SETTINGS__AUTH_AUTH0_MANAGEMENTCLIENTSECRET` |
| Plaid | `FINANCE__PERSONALFINANCE__PLAID__USEREALPLAIDAPI`, `FINANCE__PERSONALFINANCE__PLAID__BASEURL`, `FINANCE__PERSONALFINANCE__PLAID__CLIENTID`, `FINANCE__PERSONALFINANCE__PLAID__WEBHOOKURL` | `FINANCE__PERSONALFINANCE__PLAID__SECRET` |

> Full opt-in flow + failure modes for the CodeAct sandbox: [runbook](codeact-sandbox-providers.md).

If a variable is omitted or empty, runtime uses image/application defaults and any values already defined in the host platform.

## Steps
1. Set `image_version` to a known image release version.
2. Run `mode=what-if`.
3. Run `mode=deploy`.
4. Verify endpoints and telemetry.

### Arke publishing storage and channel settings

`publishingStorageEnabled` provisions dedicated private intake/delivery SMB
shares and mounts them at the same absolute paths in API and Worker. It also
keeps at least one Worker replica running for SQL outbox polling.
`publishingAppSettings` contains the non-secret `Publishing__` settings applied
identically to both hosts; the normal API/Worker app-setting collector does not
replace this parameter. Enabling storage does not enable channel admission or
publication. The dev profile stages a closed `arke-worlds-dev` channel.

For an already deployed dev release, `scripts/Configure-ArkeDevPublishing.ps1`
previews these settings against the live apps; `-Apply` applies the reviewed
profile without changing application images. Provision the storage module first.
The script is restricted to the inspected dev subscription/tenant and refuses
open channel switches. It keeps existing credentials in memory. Azure accepts
updates asynchronously: verify the new revisions are ready, compare API/Worker
settings and check health after it returns `Accepted`.

See [the Arke dev rehearsal record](arke-publishing-dev.md) for identity, verifier,
storage and Player qualification gates. Until this infrastructure change is
merged, use its reviewed branch for runtime deployments; an older template can
replace the publishing settings and mounts.

## Image source

- Primary path: successful `CI` run for a push to `master`
- Image version: the merged commit SHA
- Artifact: `image-release-<sha>` from the CI run
- Manual fallback: `.github/workflows/cd-images.yml`

## Fail-fast behavior
- Deployment fails if any required service image for the selected version is missing.
- Authentication/authorization or transport errors during ACR metadata queries fail fast with a separate message (not classified as missing tags).
- No automatic cross-service fallback is applied.
- Use `skip_image_validation=true` only for controlled emergency recovery.
