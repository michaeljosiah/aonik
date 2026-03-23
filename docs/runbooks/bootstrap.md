# Runbook: CD: Infrastructure

Workflow: `.github/workflows/cd-infra.yml`

## Purpose
Provision/update Azure platform foundations (ACR, SQL, Key Vault, observability, ACA baseline) without requiring pre-existing runtime images.

## Inputs
- `environment`: `dev|staging|prod`
- `resource_group` (optional override)
- `workload_name` (optional override)
- `location` (optional)
- `mode`: `what-if` or `deploy`
- `bootstrap_api_image` (optional API bootstrap override, port 8080)
- `bootstrap_worker_image` (optional worker bootstrap override)
- `bootstrap_adminui_image` (optional Admin UI bootstrap override, port 80)

## Required GitHub Environment Secrets/Vars
- Repo vars: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- Secrets: `SQL_ADMIN_PASSWORD`, `ACS_CONNECTION_STRING`, `VERIFICATION_HASH_KEY`
- Optional secret fallback: `AZURE_CLIENT_SECRET`
- Variable: `AZURE_RESOURCE_GROUP`

## Steps
1. Run `mode=what-if` and review.
2. Run `mode=deploy`.
3. Confirm resource creation in Azure resource group.

## Notes
- This runbook is safe for first-time bootstrap.
- It intentionally avoids any dependency on application image availability.
