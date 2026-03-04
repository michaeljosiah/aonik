# Entity Relationships

Entity relationships are defined using EF Core configurations in the Infrastructure layer.

## Where to look

- Configurations: `src/Aonik.Infrastructure/Persistence/Configurations/` (and subfolders)
- DbSets: `src/Aonik.Infrastructure/Persistence/AonikDbContext.cs`

## Notes

When adding relationships:

- Prefer explicit Fluent API configuration.
- Keep domain entities anemic (no behavior methods).
