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
            // Deliberate no-op (CLAUDE.md, narrow snapshot-reconciliation exception; precedents
            // 20260620063309 and 20260721225529).
            //
            // The scaffolded rollback CANNOT EXECUTE once the feature this migration enables has
            // been used. It re-narrows AnkHouseholdMembers.UserId to NOT NULL with a Guid.Empty
            // default and then recreates the UNFILTERED unique index on
            // (TenantId, HouseholdId, UserId). Two members without a login — the central supported
            // case of Spec 086, a family with two children — both collapse to Guid.Empty, and SQL
            // Server rejects the index with duplicate keys. The rollback would fail halfway, having
            // already destroyed the party ids those members are identified by.
            //
            // Doing nothing is both safe and sufficient. Every operation in Up is ADDITIVE apart
            // from the index, and pre-Spec-086 code neither reads the new columns nor depends on the
            // old index existing: the filtered unique indexes enforce a strict superset of what the
            // unfiltered one did for rows that have a UserId. So a code rollback over this schema
            // behaves exactly as it did before, and no data is lost.
            //
            // Reversing the SCHEMA is therefore an operational decision, not an automatic one. It is
            // only safe while no party-only member exists, and it must be done deliberately:
            //   1. confirm  SELECT COUNT(*) FROM dbo.AnkHouseholdMembers WHERE UserId IS NULL  is 0
            //   2. drop the two filtered unique indexes
            //   3. re-narrow UserId and recreate the unfiltered unique index
            //   4. drop Kind, PartyId, OwnerPartyId, MemberPartyId, ResourceKind, TermsJson
        }
    }
}
