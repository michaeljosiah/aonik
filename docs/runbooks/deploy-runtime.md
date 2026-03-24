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
- Do not place the bootstrap install code inside `API_APP_SETTINGS_JSON`.
- If the secret is omitted, the API falls back to image defaults or any value already present on the host platform.
- When the secret is configured, the Bicep template also sets `Bootstrap__Enabled=true` automatically. Both env vars are required for bootstrap to function — `Bootstrap__SetupSecret` alone is not sufficient.

### Runtime app settings payload

The runtime workflow reads optional app settings from:

- `API_APP_SETTINGS_JSON`
- `WORKER_APP_SETTINGS_JSON`

These can be provided as either GitHub Environment **Secrets** (preferred when they include credentials) or **Variables**. The workflow gives precedence to secrets when both are set.

Use JSON objects where keys are final .NET configuration environment variable names (for example, `Settings__Auth.Provider`).

Auth0 example for `API_APP_SETTINGS_JSON`:

```json
{
  "Settings__Auth.Provider": "Auth0",
  "Settings__Auth.Auth0.Domain": "aonik.uk.auth0.com",
  "Settings__Auth.Auth0.Audience": "https://api.aonik.com",
  "Settings__Auth.Auth0.ClientId": "<spa-client-id>",
  "Settings__Auth.Auth0.ManagementClientId": "<m2m-client-id>",
  "Settings__Auth.Auth0.ManagementClientSecret": "<m2m-client-secret>",
  "Settings__Auth.Auth0.Connection": "Username-Password-Authentication",
  "Settings__Auth.Auth0.ManagementAudience": "https://aonik.uk.auth0.com/api/v2/"
}
```

If these JSON payloads are omitted, runtime uses image/application defaults and any environment values already defined in the host platform.
Use these JSON payloads for non-bootstrap runtime overrides; the bootstrap install code now has a dedicated secret path.

## Steps
1. Set `image_version` to a known image release version.
2. Run `mode=what-if`.
3. Run `mode=deploy`.
4. Verify endpoints and telemetry.

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
