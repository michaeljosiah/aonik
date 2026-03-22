using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the ChatThread entity.
/// </summary>
internal class ChatThreadConfiguration : IEntityTypeConfiguration<ChatThread>
{
    public void Configure(EntityTypeBuilder<ChatThread> builder)
    {
        builder.ToTable("ChatThreads", SchemaNames.Default);

        builder.Property(x => x.Title)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.AgentName)
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.TenantId, x.UserId })
            .HasDatabaseName("IX_ChatThreads_TenantId_UserId");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.LastMessageAt })
            .HasDatabaseName("IX_ChatThreads_TenantId_UserId_LastMessageAt");

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.ChatThread)
            .HasForeignKey(x => x.ChatThreadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
