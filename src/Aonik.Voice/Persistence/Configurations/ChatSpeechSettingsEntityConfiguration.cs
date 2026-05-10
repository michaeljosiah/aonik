using Aonik.Voice.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Voice.Persistence.Configurations;

/// <summary>
/// EF model for <see cref="ChatSpeechSettingsEntity"/>. Same singleton-per-tenant shape as
/// <see cref="VoiceModeSettingsEntityConfiguration"/>; tenant id is the primary key.
/// </summary>
internal sealed class ChatSpeechSettingsEntityConfiguration : IEntityTypeConfiguration<ChatSpeechSettingsEntity>
{
    public void Configure(EntityTypeBuilder<ChatSpeechSettingsEntity> builder)
    {
        builder.ToTable("AnkChatSpeechSettings");

        builder.HasKey(x => x.TenantId);

        builder.Ignore(x => x.Id);

        builder.Property(x => x.TenantId)
            .ValueGeneratedNever();

        builder.Property(x => x.ActiveTtsProviderId)
            .HasMaxLength(100);

        builder.Property(x => x.ActiveTtsVoiceId)
            .HasMaxLength(200);

        builder.Property(x => x.ActiveTtsModelId)
            .HasMaxLength(80);

        builder.Property(x => x.Enabled)
            .IsRequired();

        builder.Property(x => x.AutoPlay)
            .IsRequired();

        builder.Property(x => x.ShowSpeakButton)
            .IsRequired();

        builder.Property(x => x.RatePercent)
            .IsRequired();
    }
}
