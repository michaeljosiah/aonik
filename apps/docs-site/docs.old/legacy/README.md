:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# AONIK Documentation

Welcome to the AONIK documentation! This guide will help you understand, develop, and deploy the AONIK financial infrastructure platform.

## Quick Links

### 🚀 Getting Started
- [Getting Started Guide](guides/getting-started.md) - Setup and first run
- [Architecture Overview](architecture/overview.md) - System architecture
- [Technology Stack](architecture/technology-stack.md) - Technologies used

### 📖 Developer Guides
- [Application Services](guides/application-services.md) - Service layer patterns
- [Domain Entities](guides/domain-entities.md) - Anemic entity model
- [API Endpoints](guides/api-endpoints.md) - FastEndpoints patterns
- [Testing](Testing.md) - Testing strategies
- [Database Migrations](guides/database-migrations.md) - EF Core migrations

#### Authentication & Authorization
- [Azure AD Setup](guides/authentication-azure-ad.md) - Configure Microsoft Entra ID
- [Auth0 Setup](guides/authentication-auth0.md) - Configure Auth0
- [Managing Roles & Permissions](guides/roles-and-permissions.md) - User access control
- [Authentication Troubleshooting](guides/authentication-troubleshooting.md) - Common issues

### 🎯 Features
- [Tenant Management](features/tenant-management.md) - Multi-tenancy implementation
- [Authentication & Authorization](features/authentication-authorization.md) - Identity and access management
- [Billing](features/billing.md) - Invoicing and billing
- [Payments](features/payments.md) - Payment processing
- [Ledger](features/ledger.md) - Double-entry accounting
- [AI Integration](features/ai-integration.md) - AI workflows

### 🏗️ Architecture
- [Clean Architecture](architecture/clean-architecture.md) - Layered architecture
- [Module Organization](architecture/module-organization.md) - Modular monolith
- [Data Flow](architecture/data-flow.md) - Request/response flow

### 🔧 Patterns & Best Practices
- [Service Layer Patterns](patterns/service-layer.md)
- [Error Handling](patterns/error-handling.md)
- [Validation](patterns/validation.md)
- [DTO Mapping](patterns/dto-mapping.md)

### 🗄️ Database
- [Schema Overview](database/schema-overview.md)
- [Entity Relationships](database/entity-relationships.md)
- [Tenant Isolation](database/tenant-isolation.md)

### 📚 Reference
- [Permissions Reference](reference/permissions.md) - Complete list of all permissions

### 📋 Architecture Decisions
- [Decision Records Index](decisions/README.md)
- [ADR-001: Custom AI Implementation](decisions/001-custom-ai-implementation-vs-maf.md)
- [ADR-002: Anemic Domain Model](decisions/002-anemic-domain-model.md)
- [ADR-003: No Generic Repository](decisions/003-no-generic-repository.md)

### 🚢 Deployment
- [Local Development](deployment/local-development.md)
- [Docker Setup](deployment/docker.md)
- [Azure Deployment](deployment/azure-deployment.md)

### 🤝 Contributing
- [Code Style Guidelines](contributing/code-style.md)
- [Git Workflow](contributing/git-workflow.md)
- [Pull Request Process](contributing/pull-requests.md)

## Documentation Organization

```
docs/
├── architecture/     # High-level system architecture
├── guides/           # How-to guides for developers
├── features/         # Feature-specific documentation
├── patterns/         # Common patterns and best practices
├── reference/        # API and permissions reference
├── api/              # API documentation
├── database/         # Database design and schema
├── decisions/        # Architecture Decision Records (ADRs)
├── deployment/       # Deployment and operations
└── contributing/     # Contribution guidelines
```

## Need Help?

- Check [AGENTS.md](https://github.com/michaeljosiah/aonik/blob/main/AGENTS.md) for AI agent coding guidelines
- Review [Architecture.md](Architecture.md) for legacy architecture docs (being migrated)
- See [decisions/](decisions/README.md) for architectural decision rationale
