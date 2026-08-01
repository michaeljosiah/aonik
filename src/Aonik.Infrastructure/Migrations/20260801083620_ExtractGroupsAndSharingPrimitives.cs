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
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refuses, rather than silently doing nothing.
            //
            // The scaffolded rollback CANNOT EXECUTE once the feature this migration enables has
            // been used: it re-narrows AnkHouseholdMembers.UserId to NOT NULL with a Guid.Empty
            // default and recreates the UNFILTERED unique index on (TenantId, HouseholdId, UserId),
            // so two members without a login — a family with two children, the central supported
            // case of Spec 086 — collapse to the same key and SQL Server rejects it. The rollback
            // would fail halfway, having already destroyed the party ids those members are
            // identified by.
            //
            // A no-op body was the first attempt and it is worse than useless: EF treats a
            // successful Down as a reversal and deletes the history row while every column and index
            // stays in place, so the next forward deployment reruns Up against objects that already
            // exist and fails there instead. Throwing keeps the history row honest.
            //
            // Reversing the schema is an operational decision, safe only while no party-only member
            // exists, and is done deliberately:
            //   1. confirm  SELECT COUNT(*) FROM dbo.AnkHouseholdMembers WHERE UserId IS NULL  is 0
            //   2. drop the two filtered unique indexes
            //   3. re-narrow UserId and recreate the unfiltered unique index
            //   4. drop Kind, PartyId, OwnerPartyId, MemberPartyId, ResourceKind, TermsJson
            //   5. delete this migration's row from __EFMigrationsHistory
            throw new InvalidOperationException(
                "ExtractGroupsAndSharingPrimitives is forward-only. Its rollback cannot run once a group "
                + "holds a member without a login, and a no-op would leave the history row claiming a "
                + "reversal that did not happen. See the comment in this migration for the manual steps.");
        }
    }
}
