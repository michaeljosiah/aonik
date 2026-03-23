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
- Required secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `SQL_ADMIN_PASSWORD`
- Optional secrets: `ACS_CONNECTION_STRING`, `VERIFICATION_HASH_KEY`, `AZURE_CLIENT_SECRET`
- Required variable: `AZURE_RESOURCE_GROUP`
- Optional variable: `WORKLOAD_NAME`

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

## Steps
1. Set `image_version` to a known image release version.
2. Run `mode=what-if`.
3. Run `mode=deploy`.
4. Verify endpoints and telemetry.

## Automatic dev deployment

- Workflow: `.github/workflows/cd-dev-auto.yml`
- Trigger: successful `CI` completion for a `push` to `master`
- Behavior: builds/pushes runtime images for the exact validated commit SHA, then deploys `dev` with digest-based references
- Prerequisite: the GitHub `dev` environment must already contain the required Azure secrets/variables and must not require manual approval for routine runtime updates

## Fail-fast behavior
- Deployment fails if any required service image for the selected version is missing.
- Authentication/authorization or transport errors during ACR metadata queries fail fast with a separate message (not classified as missing tags).
- No automatic cross-service fallback is applied.
- Use `skip_image_validation=true` only for controlled emergency recovery.


## Optional Combined Execution

If image release is not required, run `.github/workflows/cd-pipeline.yml` with `build_images=false` and set `image_version` explicitly.
