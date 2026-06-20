using Aonik.Finance.Entities.PersonalFinance;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Finance.Persistence.Configurations.PersonalFinance;

internal class CompassPlanConfiguration : IEntityTypeConfiguration<CompassPlan>
{
    public void Configure(EntityTypeBuilder<CompassPlan> builder)
    {
        builder.ToTable("CompassPlans", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PlanJson)
            .IsRequired();

        // Latest active plan per goal is the hot path (GetCurrentPlanAsync /
        // guidance), and plan history is listed per goal — both filtered by
        // tenant + user. No FK constraint to Goal: PF soft-references entities
        // (matches Goal.FundingAccountId) to keep delete behaviour simple.
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.GoalId });
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.GoalId, x.Status });
    }
}
