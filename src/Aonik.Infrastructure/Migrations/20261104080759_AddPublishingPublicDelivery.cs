using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishingPublicDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActivePublicationOperationId",
                schema: "dbo",
                table: "AnkPublicationSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicationHoldReason",
                schema: "dbo",
                table: "AnkPublicationSubmissions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessKind",
                schema: "dbo",
                table: "AnkEditorialPackageAccesses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "package");

            migrationBuilder.AddColumn<string>(
                name: "AssetId",
                schema: "dbo",
                table: "AnkEditorialPackageAccesses",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AnkEditorialDecisions_TenantId_SubmissionId_RevisionId_Id",
                schema: "dbo",
                table: "AnkEditorialDecisions",
                columns: new[] { "TenantId", "SubmissionId", "RevisionId", "Id" });

            migrationBuilder.CreateTable(
                name: "AnkPublicationOperations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpectedVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpectedSerial = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedBaseFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ExpectedSlotFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ConfirmBaseReplacement = table.Column<bool>(type: "bit", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PolicyJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    RetryCycles = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommittedSerial = table.Column<long>(type: "bigint", nullable: true),
                    CommittedSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkPublicationOperations", x => x.Id);
                    table.UniqueConstraint("AK_AnkPublicationOperations_TenantId_ChannelKey_Id", x => new { x.TenantId, x.ChannelKey, x.Id });
                    table.ForeignKey(
                        name: "FK_AnkPublicationOperations_AnkEditorialDecisions_TenantId_SubmissionId_RevisionId_DecisionId",
                        columns: x => new { x.TenantId, x.SubmissionId, x.RevisionId, x.DecisionId },
                        principalSchema: "dbo",
                        principalTable: "AnkEditorialDecisions",
                        principalColumns: new[] { "TenantId", "SubmissionId", "RevisionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkPublicationArtifacts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StorageLocationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2400)", maxLength: 2400, nullable: false),
                    ReadyAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkPublicationArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkPublicationArtifacts_AnkPublicationOperations_TenantId_ChannelKey_OperationId",
                        columns: x => new { x.TenantId, x.ChannelKey, x.OperationId },
                        principalSchema: "dbo",
                        principalTable: "AnkPublicationOperations",
                        principalColumns: new[] { "TenantId", "ChannelKey", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkPublicationCatalogueSnapshots",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Serial = table.Column<long>(type: "bigint", nullable: false),
                    PreviousSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", maxLength: 1048576, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("PK_AnkPublicationCatalogueSnapshots", x => x.Id);
                    table.UniqueConstraint("AK_AnkPublicationCatalogueSnapshots_TenantId_ChannelKey_Id", x => new { x.TenantId, x.ChannelKey, x.Id });
                    table.ForeignKey(
                        name: "FK_AnkPublicationCatalogueSnapshots_AnkPublicationCatalogueSnapshots_TenantId_ChannelKey_PreviousSnapshotId",
                        columns: x => new { x.TenantId, x.ChannelKey, x.PreviousSnapshotId },
                        principalSchema: "dbo",
                        principalTable: "AnkPublicationCatalogueSnapshots",
                        principalColumns: new[] { "TenantId", "ChannelKey", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnkPublicationCatalogueSnapshots_AnkPublicationOperations_TenantId_ChannelKey_OperationId",
                        columns: x => new { x.TenantId, x.ChannelKey, x.OperationId },
                        principalSchema: "dbo",
                        principalTable: "AnkPublicationOperations",
                        principalColumns: new[] { "TenantId", "ChannelKey", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkPublicationChannels",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Serial = table.Column<long>(type: "bigint", nullable: false),
                    HeadSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaselineSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PublicBaseUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MirrorStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MirrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MirroredSerial = table.Column<long>(type: "bigint", nullable: true),
                    MirrorClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MirrorClaimExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkPublicationChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkPublicationChannels_AnkPublicationCatalogueSnapshots_TenantId_ChannelKey_HeadSnapshotId",
                        columns: x => new { x.TenantId, x.ChannelKey, x.HeadSnapshotId },
                        principalSchema: "dbo",
                        principalTable: "AnkPublicationCatalogueSnapshots",
                        principalColumns: new[] { "TenantId", "ChannelKey", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkPublishedEditions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorldId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Slot = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatorPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BaseFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PinJson = table.Column<string>(type: "nvarchar(max)", maxLength: 65536, nullable: false),
                    IsLegacy = table.Column<bool>(type: "bit", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkPublishedEditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkPublishedEditions_AnkPublicationCatalogueSnapshots_TenantId_ChannelKey_SnapshotId",
                        columns: x => new { x.TenantId, x.ChannelKey, x.SnapshotId },
                        principalSchema: "dbo",
                        principalTable: "AnkPublicationCatalogueSnapshots",
                        principalColumns: new[] { "TenantId", "ChannelKey", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnkPublishedEditions_AnkPublicationOperations_TenantId_ChannelKey_OperationId",
                        columns: x => new { x.TenantId, x.ChannelKey, x.OperationId },
                        principalSchema: "dbo",
                        principalTable: "AnkPublicationOperations",
                        principalColumns: new[] { "TenantId", "ChannelKey", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationArtifacts_TenantId_ChannelKey_OperationId",
                schema: "dbo",
                table: "AnkPublicationArtifacts",
                columns: new[] { "TenantId", "ChannelKey", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationArtifacts_TenantId_OperationId_AssetId",
                schema: "dbo",
                table: "AnkPublicationArtifacts",
                columns: new[] { "TenantId", "OperationId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationCatalogueSnapshots_TenantId_ChannelKey_OperationId",
                schema: "dbo",
                table: "AnkPublicationCatalogueSnapshots",
                columns: new[] { "TenantId", "ChannelKey", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationCatalogueSnapshots_TenantId_ChannelKey_PreviousSnapshotId",
                schema: "dbo",
                table: "AnkPublicationCatalogueSnapshots",
                columns: new[] { "TenantId", "ChannelKey", "PreviousSnapshotId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationCatalogueSnapshots_TenantId_ChannelKey_Serial",
                schema: "dbo",
                table: "AnkPublicationCatalogueSnapshots",
                columns: new[] { "TenantId", "ChannelKey", "Serial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationCatalogueSnapshots_TenantId_OperationId",
                schema: "dbo",
                table: "AnkPublicationCatalogueSnapshots",
                columns: new[] { "TenantId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationChannels_TenantId_ChannelKey",
                schema: "dbo",
                table: "AnkPublicationChannels",
                columns: new[] { "TenantId", "ChannelKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationChannels_TenantId_ChannelKey_HeadSnapshotId",
                schema: "dbo",
                table: "AnkPublicationChannels",
                columns: new[] { "TenantId", "ChannelKey", "HeadSnapshotId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationOperations_TenantId_ChannelKey_CreatedAt",
                schema: "dbo",
                table: "AnkPublicationOperations",
                columns: new[] { "TenantId", "ChannelKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationOperations_TenantId_ClientOperationId",
                schema: "dbo",
                table: "AnkPublicationOperations",
                columns: new[] { "TenantId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationOperations_TenantId_SubmissionId",
                schema: "dbo",
                table: "AnkPublicationOperations",
                columns: new[] { "TenantId", "SubmissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationOperations_TenantId_SubmissionId_RevisionId_DecisionId",
                schema: "dbo",
                table: "AnkPublicationOperations",
                columns: new[] { "TenantId", "SubmissionId", "RevisionId", "DecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublishedEditions_TenantId_ChannelKey_OperationId",
                schema: "dbo",
                table: "AnkPublishedEditions",
                columns: new[] { "TenantId", "ChannelKey", "OperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublishedEditions_TenantId_ChannelKey_SnapshotId",
                schema: "dbo",
                table: "AnkPublishedEditions",
                columns: new[] { "TenantId", "ChannelKey", "SnapshotId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublishedEditions_TenantId_ChannelKey_WorldId_Slot",
                schema: "dbo",
                table: "AnkPublishedEditions",
                columns: new[] { "TenantId", "ChannelKey", "WorldId", "Slot" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublishedEditions_TenantId_OperationId",
                schema: "dbo",
                table: "AnkPublishedEditions",
                columns: new[] { "TenantId", "OperationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkPublicationArtifacts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPublicationChannels",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPublishedEditions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPublicationCatalogueSnapshots",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPublicationOperations",
                schema: "dbo");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AnkEditorialDecisions_TenantId_SubmissionId_RevisionId_Id",
                schema: "dbo",
                table: "AnkEditorialDecisions");

            migrationBuilder.DropColumn(
                name: "ActivePublicationOperationId",
                schema: "dbo",
                table: "AnkPublicationSubmissions");

            migrationBuilder.DropColumn(
                name: "PublicationHoldReason",
                schema: "dbo",
                table: "AnkPublicationSubmissions");

            migrationBuilder.DropColumn(
                name: "AccessKind",
                schema: "dbo",
                table: "AnkEditorialPackageAccesses");

            migrationBuilder.DropColumn(
                name: "AssetId",
                schema: "dbo",
                table: "AnkEditorialPackageAccesses");
        }
    }
}
