using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantScopeToHouseholdMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE member
SET member.TenantId = household.TenantId
FROM dbo.AnkHouseholdMembers AS member
INNER JOIN dbo.AnkHouseholds AS household ON household.Id = member.HouseholdId
WHERE member.TenantId IS NULL;
");

            migrationBuilder.Sql(@"
UPDATE member
SET member.TenantId = profile.TenantId
FROM dbo.AnkHouseholdMembers AS member
INNER JOIN dbo.AnkPersonalProfiles AS profile ON profile.UserId = member.UserId
WHERE member.TenantId IS NULL;
");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.AnkHouseholdMembers WHERE TenantId IS NULL)
    THROW 50000, 'Unable to backfill TenantId for one or more household members.', 1;
");

            migrationBuilder.AlterColumn<Guid>(
                name: "TenantId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkHouseholdMembers_TenantId_HouseholdId_UserId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                columns: new[] { "TenantId", "HouseholdId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkHouseholdMembers_TenantId_UserId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                columns: new[] { "TenantId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkHouseholdMembers_TenantId_HouseholdId_UserId",
                schema: "dbo",
                table: "AnkHouseholdMembers");

            migrationBuilder.DropIndex(
                name: "IX_AnkHouseholdMembers_TenantId_UserId",
                schema: "dbo",
                table: "AnkHouseholdMembers");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "dbo",
                table: "AnkHouseholdMembers");
        }
    }
}
