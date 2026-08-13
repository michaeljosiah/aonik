using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianshipAndVerifiableConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirthYear",
                schema: "dbo",
                table: "AnkParties",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentAgeOn",
                schema: "dbo",
                table: "AnkParties",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentBand",
                schema: "dbo",
                table: "AnkParties",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MajorityOn",
                schema: "dbo",
                table: "AnkParties",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafetyBand",
                schema: "dbo",
                table: "AnkParties",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SafetyBandChangesOn",
                schema: "dbo",
                table: "AnkParties",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkConsentGrants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantedByPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TermsVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Jurisdiction = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    VerificationMethod = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    VerificationRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevocationReason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_AnkConsentGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkConsentVerifications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuardianPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrolmentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    OutcomeRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkConsentVerifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkLegacyConsents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceBundleVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OriginalConsentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkLegacyConsents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConsentGrants_TenantId_GrantedByPartyId",
                schema: "dbo",
                table: "AnkConsentGrants",
                columns: new[] { "TenantId", "GrantedByPartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConsentGrants_TenantId_SubjectPartyId",
                schema: "dbo",
                table: "AnkConsentGrants",
                columns: new[] { "TenantId", "SubjectPartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConsentGrants_TenantId_SubjectPartyId_Purpose",
                schema: "dbo",
                table: "AnkConsentGrants",
                columns: new[] { "TenantId", "SubjectPartyId", "Purpose" },
                unique: true,
                filter: "[RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkConsentVerifications_TenantId_EnrolmentAttemptId",
                schema: "dbo",
                table: "AnkConsentVerifications",
                columns: new[] { "TenantId", "EnrolmentAttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConsentVerifications_TenantId_GuardianPartyId",
                schema: "dbo",
                table: "AnkConsentVerifications",
                columns: new[] { "TenantId", "GuardianPartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConsentVerifications_TenantId_GuardianPartyId_Succeeded",
                schema: "dbo",
                table: "AnkConsentVerifications",
                columns: new[] { "TenantId", "GuardianPartyId", "Succeeded" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkLegacyConsents_TenantId_OriginalConsentId",
                schema: "dbo",
                table: "AnkLegacyConsents",
                columns: new[] { "TenantId", "OriginalConsentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkLegacyConsents_TenantId_PartyId",
                schema: "dbo",
                table: "AnkLegacyConsents",
                columns: new[] { "TenantId", "PartyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkConsentGrants",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkConsentVerifications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkLegacyConsents",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "BirthYear",
                schema: "dbo",
                table: "AnkParties");

            migrationBuilder.DropColumn(
                name: "ConsentAgeOn",
                schema: "dbo",
                table: "AnkParties");

            migrationBuilder.DropColumn(
                name: "ConsentBand",
                schema: "dbo",
                table: "AnkParties");

            migrationBuilder.DropColumn(
                name: "MajorityOn",
                schema: "dbo",
                table: "AnkParties");

            migrationBuilder.DropColumn(
                name: "SafetyBand",
                schema: "dbo",
                table: "AnkParties");

            migrationBuilder.DropColumn(
                name: "SafetyBandChangesOn",
                schema: "dbo",
                table: "AnkParties");
        }
    }
}
