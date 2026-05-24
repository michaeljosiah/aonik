# AONIK Documentation

Welcome to the AONIK documentation. This guide covers the architecture, features, and development practices of the AONIK modular AI intelligence platform. Finance is the first shipped domain, but the platform model is broader: Core intelligence and governance capabilities support reusable domain modules and product experiences.

## Quick Links

### Getting Started
- [Getting Started Guide](guides/getting-started.md) - Setup and first run
- [Architecture Overview](architecture/overview.md) - System architecture
- [Technology Stack](architecture/technology-stack.md) - Technologies used

### Developer Guides
- [Application Services](guides/application-services.md) - Service layer patterns
- [Domain Entities](guides/domain-entities.md) - Anemic entity model
- [API Endpoints](guides/api-endpoints.md) - FastEndpoints patterns
- [Settings](guides/settings.md) - Platform settings system
- [Database Migrations](guides/database-migrations.md) - EF Core migrations
- [AONIK CLI](guides/aonik-cli.md) - Command-line interaction with AONIK systems
- [Testing](guides/Testing.md) - Testing strategies
- [Troubleshooting](guides/Troubleshooting.md) - Common issues
- [Swagger Authentication](guides/SwaggerAuthentication.md) - Testing APIs via Swagger

#### Authentication & Authorization
- [Auth0 Setup](guides/authentication-auth0.md) - Configure Auth0
- [Azure AD Setup](guides/authentication-azure-ad.md) - Configure Microsoft Entra ID
- [Auth0 Email Claim Setup](guides/auth0-email-claim-setup.md) - Email claim configuration
- [Managing Roles & Permissions](guides/roles-and-permissions.md) - User access control
- [Authentication Troubleshooting](guides/authentication-troubleshooting.md) - Common auth issues

### Features
- [Authentication & Authorization](features/authentication-authorization.md) - Identity and access management
- [Tenant Management](features/tenant-management.md) - Multi-tenancy implementation
- [Billing](features/billing.md) - Invoicing and billing
- [Payments](features/payments.md) - Payment processing
- [Ledger](features/ledger.md) - Double-entry accounting
- [Pricing](features/pricing.md) - Fee policies, FX rates, pricing quotes
- [Autonumbering](features/autonumbering.md) - Reference generation strategy
- [Individual Registration](features/individual-registration.md) - Signup orchestration
- [Payabo Registration Journey](features/payabo-registration-journey.md) - B2C onboarding flow
- [AI Integration](features/ai-integration.md) - AI workflows and agent framework
- [AI Observability](features/ai-observability.md) - AI execution monitoring and metrics
- [Financial Life Graph](features/financial-life-graph.md) - Personal-finance graph context and reasoning
- [Insight Generation Pipeline](features/insight-generation-pipeline.md) - AI-driven customer insights
- [Transaction Classification](features/transaction-classification.md) - Spending categorisation
- [Workspace](features/workspace.md) - Admin UI workspace and panel system

### Architecture
- [Architecture Overview](architecture/overview.md) - Modular monolith design
- [Module Organization](architecture/module-organization.md) - Module anatomy and boundaries
- [Data Flow](architecture/data-flow.md) - Request/response flow
- [Technology Stack](architecture/technology-stack.md) - Technologies used

### Patterns & Best Practices
- [Service Layer Patterns](patterns/service-layer.md)
- [Error Handling](patterns/error-handling.md)
- [Validation](patterns/validation.md)
- [DTO Mapping](patterns/dto-mapping.md)

### Database
- [Schema Overview](database/schema-overview.md)
- [Entity Relationships](database/entity-relationships.md)
- [Tenant Isolation](database/tenant-isolation.md)

### Reference
- [Permissions Reference](reference/permissions.md) - Complete list of all permissions

### Architecture Decisions
- [Decision Records Index](decisions/README.md)
- [ADR-001: Custom AI vs MAF](decisions/001-custom-ai-implementation-vs-maf.md) (superseded)
- [ADR-002: Anemic Domain Model](decisions/002-anemic-domain-model.md)
- [ADR-003: No Generic Repository](decisions/003-no-generic-repository.md)
- [ADR-004: Adopt Microsoft Agent Framework](decisions/004-adopt-microsoft-agent-framework.md)
- [ADR-005: Module-First Modular Monolith](decisions/005-adopt-module-first-modular-monolith.md)

### Deployment & Operations
- [Local Development](deployment/local-development.md)
- [Docker Setup](deployment/docker.md)
- [Azure Deployment](deployment/azure-deployment.md)
- [Azure IaC Roadmap](deployment/azure-iac-roadmap.md)
- [GitHub Release](deployment/github-release.md)

### Runbooks
- [Bootstrap](runbooks/bootstrap.md) - Initial platform setup
- [Build and Push](runbooks/build-and-push.md) - Container image builds
- [Deploy Runtime](runbooks/deploy-runtime.md) - Runtime deployment
- [Rollback](runbooks/rollback.md) - Rollback procedures

### Contributing
- [Code Style Guidelines](contributing/code-style.md)
- [Git Workflow](contributing/git-workflow.md)
- [Pull Request Process](contributing/pull-requests.md)

## Documentation Organization

```
docs/
├── architecture/     # High-level system architecture
├── contributing/     # Contribution guidelines
├── database/         # Database design and schema
├── decisions/        # Architecture Decision Records (ADRs)
├── deployment/       # Deployment and operations
├── features/         # Feature-specific documentation
├── guides/           # How-to guides for developers
├── images/           # Diagrams and assets
├── patterns/         # Common patterns and best practices
├── reference/        # Permissions and API reference
├── runbooks/         # Operational runbooks
├── specifications/   # Feature specifications (21+)
└── templates/        # Documentation templates
```

## Need Help?

- Check [CLAUDE.md](../CLAUDE.md) for AI agent coding guidelines and build commands
- See [decisions/](decisions/) for architectural decision rationale
- See [specifications/](specifications/) for detailed feature specifications
