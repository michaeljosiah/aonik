using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileUserMemoryEntrySupersededByDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserMemoryEntry_UserMemoryEntry_SupersededById",
                schema: "dbo",
                table: "AnkUserMemoryEntries");

            migrationBuilder.AddForeignKey(
                name: "FK_UserMemoryEntry_UserMemoryEntry_SupersededById",
                schema: "dbo",
                table: "AnkUserMemoryEntries",
                column: "SupersededById",
                principalSchema: "dbo",
                principalTable: "AnkUserMemoryEntries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a NO-OP (mirrors 20260620063309_ReconcileUserMemoryEntryMapping).
            //
            // The scaffolded Down recreated this self-referencing FK with ON DELETE SET NULL — the
            // very definition SQL Server rejects outright ("may cause cycles or multiple cascade
            // paths"), so the generated Down could never run. There is also nothing to restore: the
            // pre-migration model state (SetNull) was model-only drift that no database ever held;
            // the physical constraint has been NO ACTION since Phase1Schema created it. Rolling back
            // past this migration therefore correctly leaves the constraint exactly as it stands.
        }
    }
}
