using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spec021Compass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActivePlanId",
                schema: "dbo",
                table: "AnkGoals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoalType",
                schema: "dbo",
                table: "AnkGoals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MilestonesJson",
                schema: "dbo",
                table: "AnkGoals",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                schema: "dbo",
                table: "AnkGoals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskAppetite",
                schema: "dbo",
                table: "AnkGoals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Strategy",
                schema: "dbo",
                table: "AnkGoals",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkCompassPlans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HorizonStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HorizonEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkCompassPlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkCompassPlans_TenantId_UserId_GoalId",
                schema: "dbo",
                table: "AnkCompassPlans",
                columns: new[] { "TenantId", "UserId", "GoalId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkCompassPlans_TenantId_UserId_GoalId_Status",
                schema: "dbo",
                table: "AnkCompassPlans",
                columns: new[] { "TenantId", "UserId", "GoalId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkCompassPlans",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "ActivePlanId",
                schema: "dbo",
                table: "AnkGoals");

            migrationBuilder.DropColumn(
                name: "GoalType",
                schema: "dbo",
                table: "AnkGoals");

            migrationBuilder.DropColumn(
                name: "MilestonesJson",
                schema: "dbo",
                table: "AnkGoals");

            migrationBuilder.DropColumn(
                name: "Priority",
                schema: "dbo",
                table: "AnkGoals");

            migrationBuilder.DropColumn(
                name: "RiskAppetite",
                schema: "dbo",
                table: "AnkGoals");

            migrationBuilder.DropColumn(
                name: "Strategy",
                schema: "dbo",
                table: "AnkGoals");
        }
    }
}
