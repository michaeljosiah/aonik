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
- [Testing](guides/testing.md) - Testing strategies
- [Database Migrations](guides/database-migrations.md) - EF Core migrations

### 🎯 Features
- [Tenant Management](features/tenant-management.md) - Multi-tenancy implementation
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
├── api/              # API documentation
├── database/         # Database design and schema
├── decisions/        # Architecture Decision Records (ADRs)
├── deployment/       # Deployment and operations
└── contributing/     # Contribution guidelines
```

## Need Help?

- Check [AGENTS.md](../AGENTS.md) for AI agent coding guidelines
- Review [Architecture.md](Architecture.md) for legacy architecture docs (being migrated)
- See [decisions/](decisions/) for architectural decision rationale
