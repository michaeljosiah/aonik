using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the ChatThreadMessage entity.
/// </summary>
internal class ChatThreadMessageConfiguration : IEntityTypeConfiguration<ChatThreadMessage>
{
    public void Configure(EntityTypeBuilder<ChatThreadMessage> builder)
    {
        builder.ToTable("ChatThreadMessages", SchemaNames.Default);

        builder.Property(x => x.Role)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.AgentName)
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.ChatThreadId, x.SortOrder })
            .HasDatabaseName("IX_ChatThreadMessages_ChatThreadId_SortOrder");
    }
}
