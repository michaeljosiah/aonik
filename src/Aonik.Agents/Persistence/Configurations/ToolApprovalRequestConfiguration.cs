using Aonik.Agents.Entities;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.Agents.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ToolApprovalRequest"/> (Spec 032 §7.5 / §12). The table
/// name is finalised by the owning DbContext's table-prefix mapping (AnkToolApprovalRequests);
/// this configures the columns, the string-valued status, and the indexes that the gate's
/// match-on-resubmit lookup and the audit/queue reads rely on.
/// </summary>
internal class ToolApprovalRequestConfiguration : IEntityTypeConfiguration<ToolApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ToolApprovalRequest> builder)
    {
        builder.ToTable("ToolApprovalRequests", SchemaNames.Default);

        builder.Property(x => x.ToolName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ToolCallId)
            .HasMaxLength(200);

        builder.Property(x => x.ThreadId)
            .HasMaxLength(200);

        builder.Property(x => x.ArgsHash)
            .IsRequired()
            .HasMaxLength(64); // SHA-256 hex

        builder.Property(x => x.RiskTier)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.ActionKind)
            .HasMaxLength(400);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        // The gate's resubmit lookup: "is there a decided, unconsumed, unexpired request for this
        // tenant + tool + arguments?" Index the columns it filters on so that stays a seek.
        builder.HasIndex(x => new { x.TenantId, x.ToolName, x.ArgsHash, x.Status })
            .HasDatabaseName("IX_ToolApprovalRequest_Tenant_Tool_ArgsHash_Status");

        // Queue / audit reads by tenant + status, newest first; and expiry sweeps.
        builder.HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("IX_ToolApprovalRequest_Tenant_Status");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("IX_ToolApprovalRequest_ExpiresAt");

        builder.HasIndex(x => x.ThreadId)
            .HasDatabaseName("IX_ToolApprovalRequest_ThreadId");
    }
}
