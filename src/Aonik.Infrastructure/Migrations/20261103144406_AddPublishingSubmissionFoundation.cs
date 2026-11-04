using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishingSubmissionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkPublicationSubmissions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginatingUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AnkPublicationSubmissions", x => x.Id);
                    table.UniqueConstraint("AK_AnkPublicationSubmissions_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "AnkSubmissionRevisions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    RequestDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestDigestVersion = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatorDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    WorldId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Edition = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PackageFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DeclaredPackageBytes = table.Column<long>(type: "bigint", nullable: false),
                    DeclaredPackageSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RightsDeclarationVersion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DistributionPermissionGranted = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnkSubmissionRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkSubmissionRevisions_AnkPublicationSubmissions_TenantId_SubmissionId",
                        columns: x => new { x.TenantId, x.SubmissionId },
                        principalSchema: "dbo",
                        principalTable: "AnkPublicationSubmissions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationSubmissions_TenantId_CreatorPartyId_ClientRequestId",
                schema: "dbo",
                table: "AnkPublicationSubmissions",
                columns: new[] { "TenantId", "CreatorPartyId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPublicationSubmissions_TenantId_CreatorPartyId_CreatedAt",
                schema: "dbo",
                table: "AnkPublicationSubmissions",
                columns: new[] { "TenantId", "CreatorPartyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSubmissionRevisions_TenantId_SubmissionId_Number",
                schema: "dbo",
                table: "AnkSubmissionRevisions",
                columns: new[] { "TenantId", "SubmissionId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkSubmissionRevisions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPublicationSubmissions",
                schema: "dbo");
        }
    }
}
