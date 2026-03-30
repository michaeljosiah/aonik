using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledJobRunAndHealthSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkScheduledJobRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FireInstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnkScheduledJobRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkSchedulerHealthSnapshots",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchedulerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SchedulerInstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsStarted = table.Column<bool>(type: "bit", nullable: false),
                    InStandbyMode = table.Column<bool>(type: "bit", nullable: false),
                    ThreadPoolSize = table.Column<int>(type: "int", nullable: false),
                    ActiveJobCount = table.Column<int>(type: "int", nullable: false),
                    TotalJobCount = table.Column<int>(type: "int", nullable: false),
                    TotalTriggerCount = table.Column<int>(type: "int", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnkSchedulerHealthSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobRun_FiredAtUtc",
                schema: "dbo",
                table: "AnkScheduledJobRuns",
                column: "FiredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobRun_GroupName_JobName_FiredAtUtc",
                schema: "dbo",
                table: "AnkScheduledJobRuns",
                columns: new[] { "GroupName", "JobName", "FiredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerHealthSnapshot_Name_InstanceId",
                schema: "dbo",
                table: "AnkSchedulerHealthSnapshots",
                columns: new[] { "SchedulerName", "SchedulerInstanceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkScheduledJobRuns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkSchedulerHealthSnapshots",
                schema: "dbo");
        }
    }
}
