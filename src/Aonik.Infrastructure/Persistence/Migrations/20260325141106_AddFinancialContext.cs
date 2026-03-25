using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FinancialContextId",
                schema: "dbo",
                table: "AnkPersonalTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FinancialContexts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContextType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RelatedPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialContexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinancialContextFundingSources",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialContextId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialContextFundingSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialContextFundingSources_FinancialContexts_FinancialContextId",
                        column: x => x.FinancialContextId,
                        principalSchema: "dbo",
                        principalTable: "FinancialContexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPersonalTransactions_TenantId_UserId_FinancialContextId_OccurredAt",
                schema: "dbo",
                table: "AnkPersonalTransactions",
                columns: new[] { "TenantId", "UserId", "FinancialContextId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialContextFundingSources_FinancialContextId_PersonalAccountId",
                schema: "dbo",
                table: "FinancialContextFundingSources",
                columns: new[] { "FinancialContextId", "PersonalAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialContextFundingSources_TenantId_FinancialContextId",
                schema: "dbo",
                table: "FinancialContextFundingSources",
                columns: new[] { "TenantId", "FinancialContextId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialContexts_TenantId_UserId_ContextType",
                schema: "dbo",
                table: "FinancialContexts",
                columns: new[] { "TenantId", "UserId", "ContextType" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialContexts_TenantId_UserId_Status",
                schema: "dbo",
                table: "FinancialContexts",
                columns: new[] { "TenantId", "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialContextFundingSources",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FinancialContexts",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AnkPersonalTransactions_TenantId_UserId_FinancialContextId_OccurredAt",
                schema: "dbo",
                table: "AnkPersonalTransactions");

            migrationBuilder.DropColumn(
                name: "FinancialContextId",
                schema: "dbo",
                table: "AnkPersonalTransactions");
        }
    }
}
