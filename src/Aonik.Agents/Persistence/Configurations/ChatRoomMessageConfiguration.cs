using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

internal sealed class ChatRoomMessageConfiguration : IEntityTypeConfiguration<ChatRoomMessage>
{
    public void Configure(EntityTypeBuilder<ChatRoomMessage> builder)
    {
        builder.ToTable("ChatRoomMessages", SchemaNames.Default);
        builder.Property(x => x.ActorKind).HasMaxLength(16);
        builder.Property(x => x.AgentName).HasMaxLength(100);
        builder.Property(x => x.AuthorName).HasMaxLength(256);
        builder.Property(x => x.Content).HasMaxLength(32000);
        builder.HasIndex(x => new { x.RoomId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.RoomId, x.ClientRequestId }).IsUnique().HasFilter("[ClientRequestId] IS NOT NULL");
        builder.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}
