using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionPattern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkDecisionPatterns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Segment = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Statement = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObservationCount = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(3,2)", precision: 3, scale: 2, nullable: false),
                    LastReinforcedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupersededAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkDecisionPatterns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionPatterns_Tenant_Type_Confidence",
                schema: "dbo",
                table: "AnkDecisionPatterns",
                columns: new[] { "TenantId", "DecisionType", "Confidence" });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionPatterns_Tenant_Type_Segment_Current",
                schema: "dbo",
                table: "AnkDecisionPatterns",
                columns: new[] { "TenantId", "DecisionType", "Segment", "SupersededAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkDecisionPatterns",
                schema: "dbo");
        }
    }
}
