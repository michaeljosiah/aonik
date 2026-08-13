using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentSafetyRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkSafetyArtefacts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SafetyIncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsUnderLegalHold = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnkSafetyArtefacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkSafetyDecisions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SafetyBand = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Modality = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Layer = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Categories = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SafetyPolicyVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    GenerationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClassifierRunIds = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnonymisedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkSafetyDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkSafetyIncidents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SafetyDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    IsNonOverridable = table.Column<bool>(type: "bit", nullable: false),
                    IsUnderLegalHold = table.Column<bool>(type: "bit", nullable: false),
                    AppealState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AppealDecidedByPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppealDecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkSafetyIncidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkSafetyPolicies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SafetyBand = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ThresholdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkSafetyPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSafetyArtefacts_TenantId_ExpiresAt_IsUnderLegalHold",
                schema: "dbo",
                table: "AnkSafetyArtefacts",
                columns: new[] { "TenantId", "ExpiresAt", "IsUnderLegalHold" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSafetyArtefacts_TenantId_SafetyIncidentId",
                schema: "dbo",
                table: "AnkSafetyArtefacts",
                columns: new[] { "TenantId", "SafetyIncidentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSafetyDecisions_TenantId_ExpiresAt_AnonymisedAt",
                schema: "dbo",
                table: "AnkSafetyDecisions",
                columns: new[] { "TenantId", "ExpiresAt", "AnonymisedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSafetyDecisions_TenantId_SubjectPartyId",
                schema: "dbo",
                table: "AnkSafetyDecisions",
                columns: new[] { "TenantId", "SubjectPartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSafetyIncidents_TenantId_IsUnderLegalHold",
                schema: "dbo",
                table: "AnkSafetyIncidents",
                columns: new[] { "TenantId", "IsUnderLegalHold" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSafetyIncidents_TenantId_SafetyDecisionId",
                schema: "dbo",
                table: "AnkSafetyIncidents",
                columns: new[] { "TenantId", "SafetyDecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSafetyIncidents_TenantId_SubjectPartyId",
                schema: "dbo",
                table: "AnkSafetyIncidents",
                columns: new[] { "TenantId", "SubjectPartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSafetyPolicies_TenantId_SafetyBand_IsActive",
                schema: "dbo",
                table: "AnkSafetyPolicies",
                columns: new[] { "TenantId", "SafetyBand", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkSafetyArtefacts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkSafetyDecisions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkSafetyIncidents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkSafetyPolicies",
                schema: "dbo");
        }
    }
}
