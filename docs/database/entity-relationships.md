# Entity Relationships

Entity relationships are defined using EF Core Fluent API configurations within each module project.

## Where to look

Each domain module owns its own entity configurations:

- **Platform**: `src/Aonik.Platform/Persistence/Configurations/`
- **Finance**: `src/Aonik.Finance/Persistence/Configurations/`
- **AI**: `src/Aonik.Ai/Persistence/Configurations/`
- **Agents**: `src/Aonik.Agents/Persistence/Configurations/`
- **Infrastructure**: `src/Aonik.Infrastructure/BackgroundJobs/Entities/` (background jobs only)

Module-scoped DbContexts (`PlatformDbContext`, `FinanceDbContext`, `AiDbContext`, `AgentsDbContext`) apply configurations from their respective assemblies. The monolithic `AonikDbContext` in Infrastructure aggregates all configurations for EF Core migrations.

## Notes

When adding relationships:

- Prefer explicit Fluent API configuration over data annotations.
- Keep domain entities anemic (no behavior methods).
- Place the configuration file in the owning module's `Persistence/Configurations/` directory.
- Cross-module references use read models (e.g., `PartyReadModel` in Finance) rather than direct entity dependencies.
