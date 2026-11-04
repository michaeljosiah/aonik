using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishingPrivateIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentUploadSessionId",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentValidationReportId",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PackageVerifiedAt",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAt",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AnkSubmissionRevisions_TenantId_SubmissionId_Id",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                columns: new[] { "TenantId", "SubmissionId", "Id" });

            migrationBuilder.CreateTable(
                name: "AnkPublicationNamespaces",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorldId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatorPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedBaseFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastApprovedPartNumber = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkPublicationNamespaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkPublicationUploadQuotas",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservedBytes = table.Column<long>(type: "bigint", nullable: false),
                    ReservedPackages = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkPublicationUploadQuotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkPublicationUploadSessions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageLocationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    DeclaredBytes = table.Column<long>(type: "bigint", nullable: false),
                    DeclaredSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PartSizeBytes = table.Column<int>(type: "int", nullable: false),
                    TotalParts = table.Column<int>(type: "int", nullable: false),
                    ReceivedBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssemblyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssemblyExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReservationReleased = table.Column<bool>(type: "bit", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CleanedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkPublicationUploadSessions", x => x.Id);
                    table.UniqueConstraint("AK_AnkPublicationUploadSessions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AnkPublicationUploadSessions_AnkSubmissionRevisions_TenantId_SubmissionId_RevisionId",
                        columns: x => new { x.TenantId, x.SubmissionId, x.RevisionId },
                        principalSchema: "dbo",
                        principalTable: "AnkSubmissionRevisions",
                        principalColumns: new[] { "TenantId", "SubmissionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkPublicationValidationReports",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PackageSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PackageBytes = table.Column<long>(type: "bigint", nullable: false),
                    PolicyJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    ReaderCommit = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    VerifierImage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkPublicationValidationReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkPublicationValidationReports_AnkSubmissionRevisions_TenantId_SubmissionId_RevisionId",
                        columns: x => new { x.TenantId, x.SubmissionId, x.RevisionId },
                        principalSchema: "dbo",
                        principalTable: "AnkSubmissionRevisions",
                        principalColumns: new[] { "TenantId", "SubmissionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkPublicationUploadParts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Received = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnkPublicationUploadParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkPublicationUploadParts_AnkPublicationUploadSessions_TenantId_SessionId",
                        columns: x => new { x.TenantId, x.SessionId },
                        principalSchema: "dbo",
                        principalTable: "AnkPublicationUploadSessions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationNamespaces_TenantId_ChannelKey_WorldId",
                schema: "dbo",
                table: "AnkPublicationNamespaces",
                columns: new[] { "TenantId", "ChannelKey", "WorldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationUploadParts_TenantId_SessionId_Number",
                schema: "dbo",
                table: "AnkPublicationUploadParts",
                columns: new[] { "TenantId", "SessionId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationUploadQuotas_TenantId_OwnerPartyId",
                schema: "dbo",
                table: "AnkPublicationUploadQuotas",
                columns: new[] { "TenantId", "OwnerPartyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationUploadSessions_TenantId_CreatorPartyId_CreatedAt",
                schema: "dbo",
                table: "AnkPublicationUploadSessions",
                columns: new[] { "TenantId", "CreatorPartyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationUploadSessions_TenantId_RevisionId",
                schema: "dbo",
                table: "AnkPublicationUploadSessions",
                columns: new[] { "TenantId", "RevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationUploadSessions_TenantId_Status_ExpiresAt",
                schema: "dbo",
                table: "AnkPublicationUploadSessions",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationUploadSessions_TenantId_SubmissionId_RevisionId",
                schema: "dbo",
                table: "AnkPublicationUploadSessions",
                columns: new[] { "TenantId", "SubmissionId", "RevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationValidationReports_TenantId_RevisionId_OperationId",
                schema: "dbo",
                table: "AnkPublicationValidationReports",
                columns: new[] { "TenantId", "RevisionId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationValidationReports_TenantId_SubmissionId_RevisionId",
                schema: "dbo",
                table: "AnkPublicationValidationReports",
                columns: new[] { "TenantId", "SubmissionId", "RevisionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkPublicationNamespaces",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPublicationUploadParts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPublicationUploadQuotas",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPublicationValidationReports",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPublicationUploadSessions",
                schema: "dbo");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AnkSubmissionRevisions_TenantId_SubmissionId_Id",
                schema: "dbo",
                table: "AnkSubmissionRevisions");

            migrationBuilder.DropColumn(
                name: "CurrentUploadSessionId",
                schema: "dbo",
                table: "AnkSubmissionRevisions");

            migrationBuilder.DropColumn(
                name: "CurrentValidationReportId",
                schema: "dbo",
                table: "AnkSubmissionRevisions");

            migrationBuilder.DropColumn(
                name: "PackageVerifiedAt",
                schema: "dbo",
                table: "AnkSubmissionRevisions");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                schema: "dbo",
                table: "AnkSubmissionRevisions");
        }
    }
}
