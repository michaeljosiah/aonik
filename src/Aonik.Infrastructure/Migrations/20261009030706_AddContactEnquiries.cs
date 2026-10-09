using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContactEnquiries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkContactEnquiries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StaffRecipientEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    StaffDetailUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
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
                    table.PrimaryKey("PK_AnkContactEnquiries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkContactEnquiryAttachments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnquiryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_AnkContactEnquiryAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkContactEnquiryAttachments_AnkContactEnquiries_EnquiryId",
                        column: x => x.EnquiryId,
                        principalSchema: "dbo",
                        principalTable: "AnkContactEnquiries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkContactEnquiries_TenantId_ReceivedAtUtc_Id",
                schema: "dbo",
                table: "AnkContactEnquiries",
                columns: new[] { "TenantId", "ReceivedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkContactEnquiries_TenantId_SubmissionId",
                schema: "dbo",
                table: "AnkContactEnquiries",
                columns: new[] { "TenantId", "SubmissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkContactEnquiryAttachments_EnquiryId",
                schema: "dbo",
                table: "AnkContactEnquiryAttachments",
                column: "EnquiryId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkContactEnquiryAttachments_TenantId_EnquiryId",
                schema: "dbo",
                table: "AnkContactEnquiryAttachments",
                columns: new[] { "TenantId", "EnquiryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkContactEnquiryAttachments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkContactEnquiries",
                schema: "dbo");
        }
    }
}
