using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileUserMemoryEntryMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally a NO-OP — this is a model-only (snapshot-reconciliation) migration.
            //
            // The live table was already renamed UserMemoryEntry -> AnkUserMemoryEntries by the raw-SQL
            // migration 20260404092305_RenameUserMemoryEntryTable. EF never reflected that rename in the
            // AonikDbContext snapshot (raw SQL is opaque to it), so the snapshot had drifted. Adding the
            // MapAiTable<UserMemoryEntry> mapping + pinning the legacy constraint names lets EF regenerate a
            // CORRECT snapshot (this migration's Designer.cs) — but the only operation it can express is a
            // RenameTable that is already applied on every database. Running it would fail ("UserMemoryEntry"
            // no longer exists), so the body is empty: this migration advances the model snapshot only.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op for the same reason as Up(): the table rename is owned by the earlier
            // RenameUserMemoryEntryTable migration, not by this snapshot-only reconciliation.
        }
    }
}
