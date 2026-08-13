using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianReviewAndSafetyReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkChildSafetyPreferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreReviewEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SetByPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkChildSafetyPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkPendingContentReviews",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SafetyDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SafetyBand = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Modality = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    State = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DecidedByPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HeldAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkPendingContentReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkPreservedMaterialAccesses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SafetyIncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WasGranted = table.Column<bool>(type: "bit", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    DenialReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_AnkPreservedMaterialAccesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkSafetyEscalations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SafetyIncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedByPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("PK_AnkSafetyEscalations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkChildSafetyPreferences_TenantId_SubjectPartyId",
                schema: "dbo",
                table: "AnkChildSafetyPreferences",
                columns: new[] { "TenantId", "SubjectPartyId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPendingContentReviews_TenantId_State_ExpiresAt",
                schema: "dbo",
                table: "AnkPendingContentReviews",
                columns: new[] { "TenantId", "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPendingContentReviews_TenantId_SubjectPartyId_State",
                schema: "dbo",
                table: "AnkPendingContentReviews",
                columns: new[] { "TenantId", "SubjectPartyId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPreservedMaterialAccesses_TenantId_ActorPartyId",
                schema: "dbo",
                table: "AnkPreservedMaterialAccesses",
                columns: new[] { "TenantId", "ActorPartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPreservedMaterialAccesses_TenantId_SafetyIncidentId",
                schema: "dbo",
                table: "AnkPreservedMaterialAccesses",
                columns: new[] { "TenantId", "SafetyIncidentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSafetyEscalations_TenantId_AcknowledgedAt",
                schema: "dbo",
                table: "AnkSafetyEscalations",
                columns: new[] { "TenantId", "AcknowledgedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSafetyEscalations_TenantId_SafetyIncidentId",
                schema: "dbo",
                table: "AnkSafetyEscalations",
                columns: new[] { "TenantId", "SafetyIncidentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkChildSafetyPreferences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPendingContentReviews",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPreservedMaterialAccesses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkSafetyEscalations",
                schema: "dbo");
        }
    }
}
