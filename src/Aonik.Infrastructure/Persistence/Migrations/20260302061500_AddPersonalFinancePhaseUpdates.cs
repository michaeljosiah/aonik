using Aonik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AonikDbContext))]
[Migration("20260302061500_AddPersonalFinancePhaseUpdates")]
public partial class AddPersonalFinancePhaseUpdates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AccountSubtype",
            schema: "dbo",
            table: "AnkPersonalAccounts",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ClosedAt",
            schema: "dbo",
            table: "AnkPersonalAccounts",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsArchived",
            schema: "dbo",
            table: "AnkPersonalAccounts",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "Last4",
            schema: "dbo",
            table: "AnkPersonalAccounts",
            type: "nvarchar(4)",
            maxLength: 4,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "OpenedAt",
            schema: "dbo",
            table: "AnkPersonalAccounts",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "AiRunId",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClassificationMethod",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClassifierVersion",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Description",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ImportFingerprint",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ReviewStatus",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReviewedAt",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReviewedByUserId",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ApprovalStatus",
            schema: "dbo",
            table: "AnkCategorisationRules",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "Approved");

        migrationBuilder.AddColumn<Guid>(
            name: "AppliesToAccountId",
            schema: "dbo",
            table: "AnkCategorisationRules",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "CaseSensitive",
            schema: "dbo",
            table: "AnkCategorisationRules",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "CreatedFromUserCorrection",
            schema: "dbo",
            table: "AnkCategorisationRules",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<decimal>(
            name: "MaxAmount",
            schema: "dbo",
            table: "AnkCategorisationRules",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MatchType",
            schema: "dbo",
            table: "AnkCategorisationRules",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "contains");

        migrationBuilder.AddColumn<decimal>(
            name: "MinAmount",
            schema: "dbo",
            table: "AnkCategorisationRules",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Scope",
            schema: "dbo",
            table: "AnkCategorisationRules",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "User");

        migrationBuilder.CreateTable(
            name: "AnkStatementImports",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PersonalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                StorageUri = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Format = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                RowsTotal = table.Column<int>(type: "int", nullable: false),
                RowsParsed = table.Column<int>(type: "int", nullable: false),
                RowsImported = table.Column<int>(type: "int", nullable: false),
                RowsDuplicate = table.Column<int>(type: "int", nullable: false),
                RowsFailed = table.Column<int>(type: "int", nullable: false),
                FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AnkStatementImports", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AnkStatementImportRows",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StatementImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RowNumber = table.Column<int>(type: "int", nullable: false),
                OccurredAtRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                AmountRaw = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                DescriptionRaw = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                MerchantRaw = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                CurrencyRaw = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                NormalizedOccurredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                NormalizedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                NormalizedCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                NormalizedDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                ParseStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AnkStatementImportRows", x => x.Id);
                table.ForeignKey(
                    name: "FK_AnkStatementImportRows_AnkStatementImports_StatementImportId",
                    column: x => x.StatementImportId,
                    principalSchema: "dbo",
                    principalTable: "AnkStatementImports",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AnkPersonalAccounts_TenantId_UserId_IsArchived",
            schema: "dbo",
            table: "AnkPersonalAccounts",
            columns: new[] { "TenantId", "UserId", "IsArchived" });

        migrationBuilder.CreateIndex(
            name: "IX_AnkPersonalTransactions_ImportFingerprint",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            column: "ImportFingerprint",
            unique: true,
            filter: "[ImportFingerprint] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AnkPersonalTransactions_PersonalAccountId_OccurredAt",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            columns: new[] { "PersonalAccountId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AnkPersonalTransactions_TenantId_UserId_Category_OccurredAt",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            columns: new[] { "TenantId", "UserId", "Category", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AnkPersonalTransactions_TenantId_UserId_OccurredAt",
            schema: "dbo",
            table: "AnkPersonalTransactions",
            columns: new[] { "TenantId", "UserId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AnkStatementImportRows_StatementImportId_ParseStatus",
            schema: "dbo",
            table: "AnkStatementImportRows",
            columns: new[] { "StatementImportId", "ParseStatus" });

        migrationBuilder.CreateIndex(
            name: "IX_AnkStatementImportRows_StatementImportId_RowNumber",
            schema: "dbo",
            table: "AnkStatementImportRows",
            columns: new[] { "StatementImportId", "RowNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AnkStatementImportRows_TenantId_Fingerprint",
            schema: "dbo",
            table: "AnkStatementImportRows",
            columns: new[] { "TenantId", "Fingerprint" });

        migrationBuilder.CreateIndex(
            name: "IX_AnkStatementImports_TenantId_PersonalAccountId_CreatedAt",
            schema: "dbo",
            table: "AnkStatementImports",
            columns: new[] { "TenantId", "PersonalAccountId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_AnkStatementImports_TenantId_UserId_Status_CreatedAt",
            schema: "dbo",
            table: "AnkStatementImports",
            columns: new[] { "TenantId", "UserId", "Status", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AnkStatementImportRows",
            schema: "dbo");

        migrationBuilder.DropTable(
            name: "AnkStatementImports",
            schema: "dbo");

        migrationBuilder.DropIndex(
            name: "IX_AnkPersonalAccounts_TenantId_UserId_IsArchived",
            schema: "dbo",
            table: "AnkPersonalAccounts");

        migrationBuilder.DropIndex(
            name: "IX_AnkPersonalTransactions_ImportFingerprint",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropIndex(
            name: "IX_AnkPersonalTransactions_PersonalAccountId_OccurredAt",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropIndex(
            name: "IX_AnkPersonalTransactions_TenantId_UserId_Category_OccurredAt",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropIndex(
            name: "IX_AnkPersonalTransactions_TenantId_UserId_OccurredAt",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropColumn(
            name: "AccountSubtype",
            schema: "dbo",
            table: "AnkPersonalAccounts");

        migrationBuilder.DropColumn(
            name: "ClosedAt",
            schema: "dbo",
            table: "AnkPersonalAccounts");

        migrationBuilder.DropColumn(
            name: "IsArchived",
            schema: "dbo",
            table: "AnkPersonalAccounts");

        migrationBuilder.DropColumn(
            name: "Last4",
            schema: "dbo",
            table: "AnkPersonalAccounts");

        migrationBuilder.DropColumn(
            name: "OpenedAt",
            schema: "dbo",
            table: "AnkPersonalAccounts");

        migrationBuilder.DropColumn(
            name: "AiRunId",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropColumn(
            name: "ClassificationMethod",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropColumn(
            name: "ClassifierVersion",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropColumn(
            name: "Description",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropColumn(
            name: "ImportFingerprint",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropColumn(
            name: "ReviewStatus",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropColumn(
            name: "ReviewedAt",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropColumn(
            name: "ReviewedByUserId",
            schema: "dbo",
            table: "AnkPersonalTransactions");

        migrationBuilder.DropColumn(
            name: "ApprovalStatus",
            schema: "dbo",
            table: "AnkCategorisationRules");

        migrationBuilder.DropColumn(
            name: "AppliesToAccountId",
            schema: "dbo",
            table: "AnkCategorisationRules");

        migrationBuilder.DropColumn(
            name: "CaseSensitive",
            schema: "dbo",
            table: "AnkCategorisationRules");

        migrationBuilder.DropColumn(
            name: "CreatedFromUserCorrection",
            schema: "dbo",
            table: "AnkCategorisationRules");

        migrationBuilder.DropColumn(
            name: "MaxAmount",
            schema: "dbo",
            table: "AnkCategorisationRules");

        migrationBuilder.DropColumn(
            name: "MatchType",
            schema: "dbo",
            table: "AnkCategorisationRules");

        migrationBuilder.DropColumn(
            name: "MinAmount",
            schema: "dbo",
            table: "AnkCategorisationRules");

        migrationBuilder.DropColumn(
            name: "Scope",
            schema: "dbo",
            table: "AnkCategorisationRules");
    }
}
