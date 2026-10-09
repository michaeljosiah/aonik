using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaidAccountAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "IdentityRevision",
                schema: "dbo",
                table: "AnkUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "AnkAccountAccessActions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GuestPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    OriginalEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ExternalIssuer = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalSubject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdentityRevision = table.Column<long>(type: "bigint", nullable: true),
                    Generation = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SendWindowStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SendCount = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkAccountAccessActions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkAccountAccessActions_TenantId_Email_SendWindowStartedAtUtc",
                schema: "dbo",
                table: "AnkAccountAccessActions",
                columns: new[] { "TenantId", "Email", "SendWindowStartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkAccountAccessActions_TenantId_Purpose_PaymentIntentId",
                schema: "dbo",
                table: "AnkAccountAccessActions",
                columns: new[] { "TenantId", "Purpose", "PaymentIntentId" },
                unique: true,
                filter: "[PaymentIntentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkAccountAccessActions_TenantId_UserId_Purpose_Status",
                schema: "dbo",
                table: "AnkAccountAccessActions",
                columns: new[] { "TenantId", "UserId", "Purpose", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkAccountAccessActions",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "IdentityRevision",
                schema: "dbo",
                table: "AnkUsers");
        }
    }
}
