using Aonik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    [DbContext(typeof(AonikDbContext))]
    [Migration("20260331211500_FixScheduledJobAuditActorColumns")]
    public partial class FixScheduledJobAuditActorColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ConvertColumnToUniqueIdentifierSql("AnkScheduledJobAdminCommands", "CreatedBy"));
            migrationBuilder.Sql(ConvertColumnToUniqueIdentifierSql("AnkScheduledJobAdminCommands", "UpdatedBy"));
            migrationBuilder.Sql(ConvertColumnToUniqueIdentifierSql("AnkScheduledJobAdminCommands", "DeletedBy"));

            migrationBuilder.Sql(ConvertColumnToUniqueIdentifierSql("AnkScheduledJobProjections", "CreatedBy"));
            migrationBuilder.Sql(ConvertColumnToUniqueIdentifierSql("AnkScheduledJobProjections", "UpdatedBy"));
            migrationBuilder.Sql(ConvertColumnToUniqueIdentifierSql("AnkScheduledJobProjections", "DeletedBy"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ConvertColumnToNVarCharSql("AnkScheduledJobAdminCommands", "CreatedBy"));
            migrationBuilder.Sql(ConvertColumnToNVarCharSql("AnkScheduledJobAdminCommands", "UpdatedBy"));
            migrationBuilder.Sql(ConvertColumnToNVarCharSql("AnkScheduledJobAdminCommands", "DeletedBy"));

            migrationBuilder.Sql(ConvertColumnToNVarCharSql("AnkScheduledJobProjections", "CreatedBy"));
            migrationBuilder.Sql(ConvertColumnToNVarCharSql("AnkScheduledJobProjections", "UpdatedBy"));
            migrationBuilder.Sql(ConvertColumnToNVarCharSql("AnkScheduledJobProjections", "DeletedBy"));
        }

        private static string ConvertColumnToUniqueIdentifierSql(string tableName, string columnName) => $"""
IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NOT NULL
AND EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'[dbo].[{tableName}]')
      AND c.name = N'{columnName}'
      AND t.name = N'nvarchar'
)
BEGIN
    UPDATE [dbo].[{tableName}]
    SET [{columnName}] = NULL
    WHERE [{columnName}] IS NOT NULL
      AND TRY_CONVERT(uniqueidentifier, [{columnName}]) IS NULL;

    ALTER TABLE [dbo].[{tableName}] ALTER COLUMN [{columnName}] uniqueidentifier NULL;
END
""";

        private static string ConvertColumnToNVarCharSql(string tableName, string columnName) => $"""
IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NOT NULL
AND EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'[dbo].[{tableName}]')
      AND c.name = N'{columnName}'
      AND t.name = N'uniqueidentifier'
)
BEGIN
    ALTER TABLE [dbo].[{tableName}] ALTER COLUMN [{columnName}] nvarchar(256) NULL;
END
""";
    }
}
