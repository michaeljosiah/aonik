using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

internal sealed class ChatRoomMemberConfiguration : IEntityTypeConfiguration<ChatRoomMember>
{
    public void Configure(EntityTypeBuilder<ChatRoomMember> builder)
    {
        builder.ToTable("ChatRoomMembers", SchemaNames.Default);
        builder.Property(x => x.ActorKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.AgentName).HasMaxLength(100);
        builder.Property(x => x.DisplayName).HasMaxLength(256);
        builder.Property(x => x.Role).HasMaxLength(24);
        builder.HasIndex(x => new { x.RoomId, x.ActorKey }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.IsActive });
        builder.HasOne<ChatRoom>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}
