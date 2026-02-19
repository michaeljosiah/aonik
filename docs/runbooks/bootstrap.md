# Runbook: Azure Platform Bootstrap

Workflow: `.github/workflows/azure-platform-bootstrap.yml`

## Purpose
Provision/update Azure platform foundations (ACR, SQL, Key Vault, observability, ACA/AppService baseline) without requiring pre-existing runtime images.

## Inputs
- `profile`: `aca` or `appservice`
- `environment`: `dev|staging|prod`
- `resource_group` (optional override)
- `workload_name` (optional override)
- `location` (optional)
- `mode`: `what-if` or `deploy`
- `bootstrap_image` (optional emergency override)

## Required GitHub Environment Secrets/Vars
- Secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `SQL_ADMIN_PASSWORD`
- Optional secret fallback: `AZURE_CLIENT_SECRET`
- Variable: `AZURE_RESOURCE_GROUP`

## Steps
1. Run `mode=what-if` and review.
2. Run `mode=deploy`.
3. Confirm resource creation in Azure resource group.

## Notes
- This runbook is safe for first-time bootstrap.
- It intentionally avoids any dependency on application image availability.
