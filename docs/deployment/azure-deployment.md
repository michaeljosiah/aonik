# Azure Deployment

This guide provides a practical Azure deployment baseline for AONIK.

## Supported Deployment Profiles

- **Primary:** Azure Container Apps (ACA) for API + Worker
- **Fallback:** Azure App Service for API (worker can remain on ACA/Jobs later)

> AKS is intentionally out of scope for the current phase.

## Target Architecture (ACA Primary)

- **API**: Azure Container Apps (`Aonik.Api`) with external ingress
- **Worker**: Azure Container Apps (`Aonik.Worker`) without public ingress
- **Database**: Azure SQL Database
- **Secrets**: Azure Key Vault + managed identity access
- **Container registry**: Azure Container Registry (ACR)
- **Observability**: Log Analytics + Application Insights

## Infrastructure as Code (Bicep)

IaC assets live under `infra/`:

- `infra/profiles/aca/main.bicep`
- `infra/profiles/appservice/main.bicep`
- `infra/environments/dev|staging|prod/*.parameters.json`

See `infra/README.md` for structure, deployment commands, and required parameter substitutions.

## Required Configuration

- `ConnectionStrings:DefaultConnection` (required outside Development)
- Auth provider configuration under `Auth:*`

## Recommended Deployment Flow

1. Build and publish immutable container tags for API and Worker.
2. Provision/upgrade infrastructure with Bicep.
3. Run EF Core migrations as a controlled deployment task/job.
4. Roll out API and Worker revisions.
5. Execute post-deploy smoke checks and monitor telemetry.


## GitHub Actions CD

A dedicated workflow is available at `.github/workflows/azure-iac-cd.yml` for manual, controlled infrastructure rollout.

- Supports both profiles: `aca` and `appservice`
- Supports `what-if` mode for safe preview
- Uses OIDC-based Azure authentication (`azure/login`)
- Deploys by profile/environment parameter files in `infra/environments/*`

Set the required GitHub environment secrets before running:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `SQL_ADMIN_PASSWORD`

## Notes

- The app fails fast in non-Development environments if `ConnectionStrings:DefaultConnection` is missing.
- Avoid embedding secrets in `appsettings.json`; use environment variables and Key Vault references.
- Financially material processing should keep audit/trace telemetry enabled in all non-local environments.
