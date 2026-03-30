using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledJobAdminControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "AnkScheduledJobAdminCommands",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CommandType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResultMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkScheduledJobAdminCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkScheduledJobProjections",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NextFireTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreviousFireTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastOutcomeSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastDurationMs = table.Column<int>(type: "int", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkScheduledJobProjections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobAdminCommand_GroupName_JobName_Status",
                schema: "dbo",
                table: "AnkScheduledJobAdminCommands",
                columns: new[] { "GroupName", "JobName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobAdminCommand_Status_CreatedAt",
                schema: "dbo",
                table: "AnkScheduledJobAdminCommands",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobProjection_GroupName_JobName",
                schema: "dbo",
                table: "AnkScheduledJobProjections",
                columns: new[] { "GroupName", "JobName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkScheduledJobAdminCommands",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkScheduledJobProjections",
                schema: "dbo");
        }
    }
}
