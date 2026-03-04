---
sidebar_position: 1
---

# AONIK Documentation

Build, integrate, and operate the AONIK platform with confidence. These docs give engineers a clear picture of the architecture, core modules, and the day-to-day workflows needed to ship features and keep services healthy.

## What You Will Find Here

- **Platform overview**: how the platform is structured and how modules fit together
- **Engineering workflows**: local setup, deployments, and testing expectations
- **Reference material**: schemas, patterns, and architectural decisions
- **Operational guidance**: troubleshooting, diagnostics, and runtime tips

## Quick Start

1. Review the project context in **[README.md](https://github.com/michaeljosiah/aonik/blob/main/README.md)**.
2. Set up your environment with **[Getting Started](guides/getting-started.md)**.
3. Run the platform locally using **[Local Development](deployment/local-development.md)** or **[Docker Setup](deployment/docker.md)**.

## Architecture At A Glance

AONIK follows Clean Architecture with an anemic domain model. Business logic lives in application services, while modules are organized vertically by domain (Billing, Payments, Ledger, AI).

- **Domain**: entity definitions and primitives
- **Application**: service layer, DTOs, workflows
- **Infrastructure**: EF Core, external integrations, providers
- **API**: FastEndpoints endpoints and contracts
- **Worker**: background jobs and scheduled tasks

## Documentation Map

### Core Engineering Docs

- **[Architecture](Architecture.md)**
- **[Testing](Testing.md)**
- **[Troubleshooting](Troubleshooting.md)**
- **[Swagger Authentication](SwaggerAuthentication.md)**

### Features

- **[Pricing & FX Quotes](features/pricing.md)**

### Deployment & Operations

- **[Local Development](deployment/local-development.md)**
- **[Docker Setup](deployment/docker.md)**
- **[Database Overview](database/schema-overview.md)**
- **[Tenant Isolation](database/tenant-isolation.md)**

### Patterns & Decisions

- **[Service Layer Patterns](patterns/service-layer.md)**
- **[Architecture Decisions](decisions/README.md)**
- **[User Onboarding Requirements](requirements/user-onboarding-specification.md)**

## Reference Links

- **[AGENTS.md](https://github.com/michaeljosiah/aonik/blob/main/AGENTS.md)**: coding standards and build commands
- **[CHANGELOG.md](https://github.com/michaeljosiah/aonik/blob/main/CHANGELOG.md)**: release notes and platform history

## Need Help?

Start with the **[Troubleshooting Guide](Troubleshooting.md)**, then check recent changes in the **[CHANGELOG](https://github.com/michaeljosiah/aonik/blob/main/CHANGELOG.md)**. If something is missing or outdated, open an issue or submit a PR.

---

*Last updated: January 8, 2025*
