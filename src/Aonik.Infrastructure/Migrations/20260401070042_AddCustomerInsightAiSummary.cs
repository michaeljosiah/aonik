using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerInsightAiSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkCustomerInsightAiSummaries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerInsightSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AsOfUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NarrativeVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupersededById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AnkCustomerInsightAiSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkCustomerInsightAiSummaries_AnkAiRuns_AiRunId",
                        column: x => x.AiRunId,
                        principalSchema: "dbo",
                        principalTable: "AnkAiRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnkCustomerInsightAiSummaries_AnkCustomerInsightAiSummaries_SupersededById",
                        column: x => x.SupersededById,
                        principalSchema: "dbo",
                        principalTable: "AnkCustomerInsightAiSummaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInsightAiSummaries_AiRunId",
                schema: "dbo",
                table: "AnkCustomerInsightAiSummaries",
                column: "AiRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInsightAiSummaries_Snapshot_Current",
                schema: "dbo",
                table: "AnkCustomerInsightAiSummaries",
                columns: new[] { "CustomerInsightSnapshotId", "Status" },
                filter: "[Status] = 'Current'");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInsightAiSummaries_SupersededById",
                schema: "dbo",
                table: "AnkCustomerInsightAiSummaries",
                column: "SupersededById");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInsightAiSummaries_TenantUser_Current",
                schema: "dbo",
                table: "AnkCustomerInsightAiSummaries",
                columns: new[] { "TenantId", "UserId", "Status" },
                filter: "[Status] = 'Current'");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkCustomerInsightAiSummaries",
                schema: "dbo");

        }
    }
}
