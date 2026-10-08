using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceOrderDeliveryDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkOrderDeliveryDetails",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaserEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    PurchaserFirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PurchaserLastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PurchaserPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Postcode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(201)", maxLength: 201, nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AnkOrderDeliveryDetails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderDeliveryDetails_TenantId_DeliveryDate_OrderId",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                columns: new[] { "TenantId", "DeliveryDate", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderDeliveryDetails_TenantId_OrderId",
                schema: "dbo",
                table: "AnkOrderDeliveryDetails",
                columns: new[] { "TenantId", "OrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkOrderDeliveryDetails",
                schema: "dbo");
        }
    }
}
