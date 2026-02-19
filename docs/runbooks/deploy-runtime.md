# Runbook: Deploy Runtime

Workflow: `.github/workflows/azure-runtime-deploy.yml`

## Purpose
Deploy ACA/AppService runtime updates using one explicit image release version.

## Inputs
- `profile`: `aca` or `appservice`
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
- Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `SQL_ADMIN_PASSWORD`
- Optional secret fallback: `AZURE_CLIENT_SECRET`
- Variable: `AZURE_RESOURCE_GROUP`

## Steps
1. Set `image_version` to a known image release version.
2. Run `mode=what-if`.
3. Run `mode=deploy`.
4. Verify endpoints and telemetry.

## Fail-fast behavior
- Deployment fails if any required service image for the selected version is missing.
- No automatic cross-service fallback is applied.
- Use `skip_image_validation=true` only for controlled emergency recovery.
