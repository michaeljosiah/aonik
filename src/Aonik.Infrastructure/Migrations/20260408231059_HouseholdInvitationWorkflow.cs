using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HouseholdInvitationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationStatus",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Accepted");

            migrationBuilder.AddColumn<DateTime>(
                name: "InvitedAt",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedByUserId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "dbo",
                table: "AnkHouseholdMembers");

            migrationBuilder.DropColumn(
                name: "InvitationStatus",
                schema: "dbo",
                table: "AnkHouseholdMembers");

            migrationBuilder.DropColumn(
                name: "InvitedAt",
                schema: "dbo",
                table: "AnkHouseholdMembers");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                schema: "dbo",
                table: "AnkHouseholdMembers");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                schema: "dbo",
                table: "AnkHouseholdMembers");
        }
    }
}
