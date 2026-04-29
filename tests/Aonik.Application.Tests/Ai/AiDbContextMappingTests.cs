using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Ai;

public class AiDbContextMappingTests
{
    [Fact]
    public void TenantAgentSettings_Should_MapToCanonicalAiTableName()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"AiDbContextMapping_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new AiDbContext(options);

        var entityType = dbContext.Model.FindEntityType(typeof(TenantAgentSettings));

        entityType.Should().NotBeNull();
        entityType!.GetSchema().Should().Be("dbo");
        entityType.GetTableName().Should().Be("AnkTenantAgentSettings");
    }
}
