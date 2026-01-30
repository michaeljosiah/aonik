using System;
using Aonik.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AonikDbContext))]
    [Migration("20260130090000_AddFxManagementTables")]
    public partial class AddFxManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FxRateSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    RefreshIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    LastFetchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_FxRateSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FxRefreshSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_FxRefreshSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FxSpreadPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TargetCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CustomerTier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MarkupBps = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    MinSpreadPercent = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    MaxSpreadPercent = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_FxSpreadPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FxRateSources_Status",
                table: "FxRateSources",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FxRateSources_TenantId",
                table: "FxRateSources",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRateSources_TenantId_Name",
                table: "FxRateSources",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FxRefreshSchedules_IsEnabled",
                table: "FxRefreshSchedules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_FxRefreshSchedules_TenantId",
                table: "FxRefreshSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FxRefreshSchedules_TenantId_Name",
                table: "FxRefreshSchedules",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FxSpreadPolicies_Status",
                table: "FxSpreadPolicies",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FxSpreadPolicies_TenantId",
                table: "FxSpreadPolicies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FxSpreadPolicies_TenantId_BaseCurrency_TargetCurrency_CustomerTier",
                table: "FxSpreadPolicies",
                columns: new[] { "TenantId", "BaseCurrency", "TargetCurrency", "CustomerTier" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FxRateSources");

            migrationBuilder.DropTable(
                name: "FxRefreshSchedules");

            migrationBuilder.DropTable(
                name: "FxSpreadPolicies");
        }
    }
}
