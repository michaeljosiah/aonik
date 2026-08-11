using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalLedgerFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The scaffolded AlterColumn on RowVersion (varbinary(max) -> rowversion) is omitted
            // deliberately: it is snapshot drift, not a real schema change, and SQL Server cannot
            // execute it in any case.
            //
            // AnkLedgers.RowVersion has been a rowversion column since 20260328233909_InitialCreate
            // created it as `type: "rowversion", rowVersion: true`, and no migration has altered it
            // since. The prior model snapshot recorded it as varbinary(max) without a concurrency
            // token — model-only drift that no database ever held. Adding a LedgerConfiguration in
            // this migration brought the entity back through the differ, which then scaffolded a
            // "correction" toward a state the physical column already has. SQL Server rejects
            // ALTER COLUMN ... rowversion outright, so leaving it in would make this migration
            // unrunnable.
            //
            // The .Designer.cs snapshot is untouched tool output and now records the correct
            // rowversion + concurrency-token shape, which is the actual fix. Narrow exception per
            // CLAUDE.md; precedents 20260620063309_ReconcileUserMemoryEntryMapping and
            // 20260721225529_ReconcileUserMemoryEntrySupersededByDeleteBehavior.

            migrationBuilder.AddColumn<bool>(
                name: "IsCanonical",
                schema: "dbo",
                table: "AnkLedgers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AnkLedgers_TenantId",
                schema: "dbo",
                table: "AnkLedgers",
                column: "TenantId",
                unique: true,
                filter: "[IsCanonical] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkLedgers_TenantId",
                schema: "dbo",
                table: "AnkLedgers");

            migrationBuilder.DropColumn(
                name: "IsCanonical",
                schema: "dbo",
                table: "AnkLedgers");


            // No RowVersion alteration to reverse — see Up. The column is, and always was,
            // a rowversion; rolling back past this migration correctly leaves it that way.
        }
    }
}
