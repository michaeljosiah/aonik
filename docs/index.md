# Documentation Index

Welcome to the AONIK documentation. This index provides quick access to all project documentation.

## Getting Started

- **[README.md](../README.md)** - Project overview, quick start guide, and vision

## For Developers

### Local Development

- **[Local Development](deployment/local-development.md)**
- **[Docker Setup](deployment/docker.md)**

### Core Documentation

- **[AGENTS.md](../AGENTS.md)** - Comprehensive coding guidelines for AI agents and developers
  - Build commands and workflows
  - Architecture patterns (Clean Architecture with Anemic Domain Model)
  - Code style guidelines
  - Entity and service patterns
  - Pre-commit checklist

### Getting Started

- **[Getting Started Guide](guides/getting-started.md)** - Setup and first run
- **[Settings Guide](guides/settings.md)** - Settings scopes and endpoints

### Features

- **[Pricing & FX Quotes](features/pricing.md)** - How fees, FX, and limits work

### Testing

- **[Testing Guide](Testing.md)** - Complete testing documentation
  - Testing philosophy and structure
  - How to run tests
  - Writing service tests with mocks
  - Test patterns for anemic entities
  - Common testing pitfalls

### Troubleshooting

- **[Troubleshooting Guide](Troubleshooting.md)** - Solutions to common issues
  - Build errors and fixes
  - Test failure diagnostics
  - Runtime issues
  - Database problems
  - NuGet package conflicts
  - Quick fixes checklist

## Project Information

- **[CHANGELOG.md](../CHANGELOG.md)** - Version history and recent changes
  - Latest fixes (January 2025)
  - Build system updates
  - Entity Framework configuration corrections
  - Test infrastructure improvements

## Quick Links

### Common Tasks

- **Build the solution**: `dotnet build Aonik.sln`
- **Run tests**: `dotnet test Aonik.sln`
- **Run API**: `dotnet run --project src/Aonik.Api`
- **Create migration**: `dotnet ef migrations add <Name> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api`

### Local Development & Docker

- **Local Development**: `deployment/local-development.md`
- **Docker Setup**: `deployment/docker.md`

### Need Help?

1. Check the [Troubleshooting Guide](Troubleshooting.md) for your specific issue
2. Review the [CHANGELOG](../CHANGELOG.md) for recent changes
3. Consult [AGENTS.md](../AGENTS.md) for coding patterns
4. Search GitHub issues
5. Create a new issue with details

## Documentation Status

| Document | Status | Last Updated |
|----------|--------|--------------|
| README.md | ✅ Current | Jan 8, 2025 |
| AGENTS.md | ✅ Current | Jan 8, 2025 |
| CHANGELOG.md | ✅ Current | Jan 8, 2025 |
| docs/Testing.md | ✅ Current | Jan 8, 2025 |
| docs/Troubleshooting.md | ✅ Current | Jan 8, 2025 |
| docs/guides/settings.md | ✅ Current | Jan 11, 2026 |

## Project Structure

```
aonik/
├── src/
│   ├── Aonik.SharedKernel/     # Common primitives (Entity, Result<T>, etc.)
│   ├── Aonik.Domain/            # Domain entities (anemic model)
│   ├── Aonik.Application/       # Business logic and services
│   ├── Aonik.Infrastructure/    # EF Core, external services, AI providers
│   ├── Aonik.Api/              # FastEndpoints HTTP API
│   └── Aonik.Worker/           # Background jobs
├── tests/
│   ├── Aonik.Domain.Tests/
│   ├── Aonik.Application.Tests/
│   ├── Aonik.Infrastructure.Tests/
│   └── Aonik.Api.Tests/
├── docs/                        # Documentation (this directory)
│   ├── index.md                # This file
│   ├── Testing.md              # Testing guide
│   └── Troubleshooting.md      # Common issues
├── AGENTS.md                    # Coding guidelines
├── CHANGELOG.md                 # Version history
├── README.md                    # Project overview
└── Aonik.sln                    # Solution file
```

## Architecture Overview

AONIK follows **Clean Architecture** principles with these key characteristics:

### Anemic Domain Model
- Domain entities are simple data containers
- No business logic in entities
- All business logic in application services

### Vertical Slicing
- Code organized by business modules (Billing, Payments, Ledger, AI)
- Each module has entities, services, and endpoints

### Key Technologies
- **.NET 10** - Latest .NET platform
- **FastEndpoints** - High-performance HTTP endpoints
- **Entity Framework Core 10** - ORM and data access
- **xUnit + FluentAssertions** - Testing framework

## Contributing

When contributing to the documentation:

1. Keep it concise and scannable
2. Use code examples where helpful
3. Update this index when adding new documents
4. Follow the existing structure and tone
5. Test all commands and examples
6. Update the "Last Updated" date

## Feedback

Documentation improvements are always welcome! If you find:
- Unclear explanations
- Missing information
- Outdated content
- Broken links

Please create an issue or submit a pull request.

---

*Last updated: January 11, 2026*
