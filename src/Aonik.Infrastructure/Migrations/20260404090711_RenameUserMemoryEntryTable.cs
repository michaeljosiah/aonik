using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameUserMemoryEntryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase1Schema created this table as dbo.UserMemoryEntry but the EF model
            // maps it to dbo.AnkUserMemoryEntries via the standard Ank prefix convention.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'UserMemoryEntry')
   AND NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AnkUserMemoryEntries')
BEGIN
    EXEC sp_rename 'dbo.UserMemoryEntry', 'AnkUserMemoryEntries';
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AnkUserMemoryEntries')
   AND NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'UserMemoryEntry')
BEGIN
    EXEC sp_rename 'dbo.AnkUserMemoryEntries', 'UserMemoryEntry';
END");
        }
    }
}
