# Azure Deployment

This guide is a starter outline for deploying AONIK to Azure. It will evolve as the platform stabilizes.

## Target Architecture (Typical)

- **API**: Azure App Service (or Container Apps)
- **Database**: Azure SQL Database (or SQL Server on VM)
- **Secrets**: Azure Key Vault
- **Observability**: Application Insights / OTLP collector

## Required Configuration

- `ConnectionStrings:DefaultConnection` (required outside Development)
- Auth provider configuration under `Auth:*`

## Recommended Approach

1. Provision a SQL Server-compatible database.
2. Configure the connection string via App Service configuration (env vars).
3. Run EF Core migrations during deployment (pipeline step) or as a controlled release task.

## Notes

- The app fails fast in non-Development environments if `ConnectionStrings:DefaultConnection` is missing.
- Avoid embedding secrets in `appsettings.json`; use environment variables and Key Vault.
