using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcilePaymentRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Snapshot reconciliation only, under the AGENTS.md narrow no-op exception.
            // InitialCreate (20260328233909, line 1742) created this as native rowversion;
            // no subsequent migration altered AnkPayments. The old canonical snapshot
            // discovered Payment only after shared rowversion configuration and recorded
            // ordinary binary instead. Registering its DbSet restores the real mapping.
            // SQL Server cannot ALTER an existing timestamp/rowversion column this way.
            // Designer.cs and the snapshot remain untouched EF CLI output.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No physical change in Up; retain the native rowversion from InitialCreate.
        }
    }
}
