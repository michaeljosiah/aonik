using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

internal class CommitmentCycleConfiguration : IEntityTypeConfiguration<CommitmentCycle>
{
    public void Configure(EntityTypeBuilder<CommitmentCycle> builder)
    {
        builder.ToTable("CommitmentCycles", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.SkipReason)
            .HasMaxLength(500);

        // Timeline + "never missed" reads off (commitment, status).
        builder.HasIndex(x => new { x.TenantId, x.CommitmentId, x.Status });
    }
}
