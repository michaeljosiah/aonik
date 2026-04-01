using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerInsightSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkCustomerInsightSnapshots",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AsOfUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    GenerationDurationMs = table.Column<int>(type: "int", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SupersededById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkCustomerInsightSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkCustomerInsightSnapshots_AnkCustomerInsightSnapshots_SupersededById",
                        column: x => x.SupersededById,
                        principalSchema: "dbo",
                        principalTable: "AnkCustomerInsightSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInsightSnapshots_SupersededById",
                schema: "dbo",
                table: "AnkCustomerInsightSnapshots",
                column: "SupersededById");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInsightSnapshots_TenantUser_AsOfUtc",
                schema: "dbo",
                table: "AnkCustomerInsightSnapshots",
                columns: new[] { "TenantId", "UserId", "AsOfUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInsightSnapshots_TenantUser_Current",
                schema: "dbo",
                table: "AnkCustomerInsightSnapshots",
                columns: new[] { "TenantId", "UserId", "Status" },
                filter: "[Status] = 'Current'");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInsightSnapshots_TenantUser_SourceHash",
                schema: "dbo",
                table: "AnkCustomerInsightSnapshots",
                columns: new[] { "TenantId", "UserId", "SourceHash" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkCustomerInsightSnapshots",
                schema: "dbo");

        }
    }
}
