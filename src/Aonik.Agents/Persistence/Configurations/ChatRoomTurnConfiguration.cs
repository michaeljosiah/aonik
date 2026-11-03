using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

internal sealed class ChatRoomTurnConfiguration : IEntityTypeConfiguration<ChatRoomTurn>
{
    public void Configure(EntityTypeBuilder<ChatRoomTurn> builder)
    {
        builder.ToTable("ChatRoomTurns", SchemaNames.Default);
        builder.Property(x => x.AgentName).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(24);
        builder.Property(x => x.Error).HasMaxLength(300);
        builder.HasIndex(x => new { x.RoomId, x.ClientRequestId }).IsUnique();
        builder.HasIndex(x => new { x.RoomId, x.StartedAt });
        builder.HasIndex(x => new { x.TenantId, x.AiRunId });
        builder.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}
