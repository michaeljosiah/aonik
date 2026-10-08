using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileOrderFundingRefRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Snapshot reconciliation only, under the AGENTS.md narrow no-op exception.
            // InitialCreate (20260328233909, line 1487) created this column as native rowversion;
            // no subsequent migration altered it. Phase1Schema's Designer recorded plain binary
            // data even though its SQL never touched this table. Registering OrderFundingRef in
            // the canonical context restores the shared rowversion mapping before table naming.
            // The scaffolded ALTER cannot run on SQL Server and describes no physical change.
            // Designer.cs and the model snapshot remain untouched CLI output. See the same
            // reconciliation in 20260731221303_AddCanonicalLedgerFlag.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing physical changed in Up. Retain the native rowversion from InitialCreate.
        }
    }
}
