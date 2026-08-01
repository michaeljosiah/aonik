using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtractGroupsAndSharingPrimitives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkHouseholdMembers_TenantId_HouseholdId_UserId",
                schema: "dbo",
                table: "AnkHouseholdMembers");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "dbo",
                table: "AnkHouseholds",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "PartyId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerPartyId",
                schema: "dbo",
                table: "AnkCircleInvites",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceKind",
                schema: "dbo",
                table: "AnkCircleInvites",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TermsJson",
                schema: "dbo",
                table: "AnkCircleInvites",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MemberPartyId",
                schema: "dbo",
                table: "AnkCircleGrants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerPartyId",
                schema: "dbo",
                table: "AnkCircleGrants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceKind",
                schema: "dbo",
                table: "AnkCircleGrants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TermsJson",
                schema: "dbo",
                table: "AnkCircleGrants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkHouseholdMembers_TenantId_HouseholdId_PartyId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                columns: new[] { "TenantId", "HouseholdId", "PartyId" },
                unique: true,
                filter: "[PartyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkHouseholdMembers_TenantId_HouseholdId_UserId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                columns: new[] { "TenantId", "HouseholdId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkHouseholdMembers_TenantId_HouseholdId_PartyId",
                schema: "dbo",
                table: "AnkHouseholdMembers");

            migrationBuilder.DropIndex(
                name: "IX_AnkHouseholdMembers_TenantId_HouseholdId_UserId",
                schema: "dbo",
                table: "AnkHouseholdMembers");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "dbo",
                table: "AnkHouseholds");

            migrationBuilder.DropColumn(
                name: "PartyId",
                schema: "dbo",
                table: "AnkHouseholdMembers");

            migrationBuilder.DropColumn(
                name: "OwnerPartyId",
                schema: "dbo",
                table: "AnkCircleInvites");

            migrationBuilder.DropColumn(
                name: "ResourceKind",
                schema: "dbo",
                table: "AnkCircleInvites");

            migrationBuilder.DropColumn(
                name: "TermsJson",
                schema: "dbo",
                table: "AnkCircleInvites");

            migrationBuilder.DropColumn(
                name: "MemberPartyId",
                schema: "dbo",
                table: "AnkCircleGrants");

            migrationBuilder.DropColumn(
                name: "OwnerPartyId",
                schema: "dbo",
                table: "AnkCircleGrants");

            migrationBuilder.DropColumn(
                name: "ResourceKind",
                schema: "dbo",
                table: "AnkCircleGrants");

            migrationBuilder.DropColumn(
                name: "TermsJson",
                schema: "dbo",
                table: "AnkCircleGrants");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkHouseholdMembers_TenantId_HouseholdId_UserId",
                schema: "dbo",
                table: "AnkHouseholdMembers",
                columns: new[] { "TenantId", "HouseholdId", "UserId" },
                unique: true);
        }
    }
}
