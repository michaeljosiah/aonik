using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishingEditorialReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkPublicationValidationReports_TenantId_SubmissionId_RevisionId",
                schema: "dbo",
                table: "AnkPublicationValidationReports");

            migrationBuilder.AddColumn<string>(
                name: "EditorialMaterialJson",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                type: "nvarchar(max)",
                maxLength: 102400,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginatingUserId",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevisionCommandDigest",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevisionOperationId",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveEditorialDecisionId",
                schema: "dbo",
                table: "AnkPublicationSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AnkPublicationValidationReports_TenantId_SubmissionId_RevisionId_Id",
                schema: "dbo",
                table: "AnkPublicationValidationReports",
                columns: new[] { "TenantId", "SubmissionId", "RevisionId", "Id" });

            migrationBuilder.CreateTable(
                name: "AnkEditorialPackageAccesses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkEditorialPackageAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkEditorialPackageAccesses_AnkSubmissionRevisions_TenantId_SubmissionId_RevisionId",
                        columns: x => new { x.TenantId, x.SubmissionId, x.RevisionId },
                        principalSchema: "dbo",
                        principalTable: "AnkSubmissionRevisions",
                        principalColumns: new[] { "TenantId", "SubmissionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkEditorialReviewSnapshots",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidationReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DigestVersion = table.Column<int>(type: "int", nullable: false),
                    MaterialJson = table.Column<string>(type: "nvarchar(max)", maxLength: 524288, nullable: false),
                    PolicyJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
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
                    table.PrimaryKey("PK_AnkEditorialReviewSnapshots", x => x.Id);
                    table.UniqueConstraint("AK_AnkEditorialReviewSnapshots_TenantId_SubmissionId_RevisionId_Id", x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.Id });
                    table.ForeignKey(
                        name: "FK_AnkEditorialReviewSnapshots_AnkPublicationValidationReports_TenantId_SubmissionId_RevisionId_ValidationReportId",
                        columns: x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.ValidationReportId },
                        principalSchema: "dbo",
                        principalTable: "AnkPublicationValidationReports",
                        principalColumns: new[] { "TenantId", "SubmissionId", "RevisionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkEditorialDecisions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReviewDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpectedVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InternalReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ChecksJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkEditorialDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkEditorialDecisions_AnkEditorialReviewSnapshots_TenantId_SubmissionId_RevisionId_SnapshotId",
                        columns: x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.SnapshotId },
                        principalSchema: "dbo",
                        principalTable: "AnkEditorialReviewSnapshots",
                        principalColumns: new[] { "TenantId", "SubmissionId", "RevisionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSubmissionRevisions_TenantId_SubmissionId_RevisionOperationId",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                columns: new[] { "TenantId", "SubmissionId", "RevisionOperationId" },
                unique: true,
                filter: "[RevisionOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkEditorialDecisions_TenantId_SubmissionId_DecidedAt",
                schema: "dbo",
                table: "AnkEditorialDecisions",
                columns: new[] { "TenantId", "SubmissionId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkEditorialDecisions_TenantId_SubmissionId_OperationId",
                schema: "dbo",
                table: "AnkEditorialDecisions",
                columns: new[] { "TenantId", "SubmissionId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkEditorialDecisions_TenantId_SubmissionId_RevisionId_SnapshotId",
                schema: "dbo",
                table: "AnkEditorialDecisions",
                columns: new[] { "TenantId", "SubmissionId", "RevisionId", "SnapshotId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkEditorialPackageAccesses_TenantId_SubmissionId_AccessedAt",
                schema: "dbo",
                table: "AnkEditorialPackageAccesses",
                columns: new[] { "TenantId", "SubmissionId", "AccessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkEditorialPackageAccesses_TenantId_SubmissionId_RevisionId",
                schema: "dbo",
                table: "AnkEditorialPackageAccesses",
                columns: new[] { "TenantId", "SubmissionId", "RevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkEditorialReviewSnapshots_TenantId_RevisionId_ReviewDigest",
                schema: "dbo",
                table: "AnkEditorialReviewSnapshots",
                columns: new[] { "TenantId", "RevisionId", "ReviewDigest" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkEditorialReviewSnapshots_TenantId_SubmissionId_RevisionId_ValidationReportId",
                schema: "dbo",
                table: "AnkEditorialReviewSnapshots",
                columns: new[] { "TenantId", "SubmissionId", "RevisionId", "ValidationReportId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkEditorialDecisions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkEditorialPackageAccesses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkEditorialReviewSnapshots",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AnkSubmissionRevisions_TenantId_SubmissionId_RevisionOperationId",
                schema: "dbo",
                table: "AnkSubmissionRevisions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AnkPublicationValidationReports_TenantId_SubmissionId_RevisionId_Id",
                schema: "dbo",
                table: "AnkPublicationValidationReports");

            migrationBuilder.DropColumn(
                name: "EditorialMaterialJson",
                schema: "dbo",
                table: "AnkSubmissionRevisions");

            migrationBuilder.DropColumn(
                name: "OriginatingUserId",
                schema: "dbo",
                table: "AnkSubmissionRevisions");

            migrationBuilder.DropColumn(
                name: "RevisionCommandDigest",
                schema: "dbo",
                table: "AnkSubmissionRevisions");

            migrationBuilder.DropColumn(
                name: "RevisionOperationId",
                schema: "dbo",
                table: "AnkSubmissionRevisions");

            migrationBuilder.DropColumn(
                name: "ActiveEditorialDecisionId",
                schema: "dbo",
                table: "AnkPublicationSubmissions");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationValidationReports_TenantId_SubmissionId_RevisionId",
                schema: "dbo",
                table: "AnkPublicationValidationReports",
                columns: new[] { "TenantId", "SubmissionId", "RevisionId" });
        }
    }
}
