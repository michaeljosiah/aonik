using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceDeliveryCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderStartDeadlineUtc",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkCartDeliveryReservations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapacityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SelectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PaymentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkCartDeliveryReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkDeliveryDateCapacities",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkDeliveryDateCapacities", x => x.Id);
                    table.CheckConstraint("CK_DeliveryDateCapacity_Nonnegative", "[Capacity] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkCartDeliveryReservations_TenantId_CapacityId_Status_ExpiresAtUtc",
                schema: "dbo",
                table: "AnkCartDeliveryReservations",
                columns: new[] { "TenantId", "CapacityId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkCartDeliveryReservations_TenantId_CartId",
                schema: "dbo",
                table: "AnkCartDeliveryReservations",
                columns: new[] { "TenantId", "CartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkCartDeliveryReservations_TenantId_OrderId",
                schema: "dbo",
                table: "AnkCartDeliveryReservations",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkCartDeliveryReservations_TenantId_Status_PaymentDeadlineUtc",
                schema: "dbo",
                table: "AnkCartDeliveryReservations",
                columns: new[] { "TenantId", "Status", "PaymentDeadlineUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkDeliveryDateCapacities_TenantId_DeliveryDate",
                schema: "dbo",
                table: "AnkDeliveryDateCapacities",
                columns: new[] { "TenantId", "DeliveryDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkCartDeliveryReservations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkDeliveryDateCapacities",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "ProviderStartDeadlineUtc",
                schema: "dbo",
                table: "AnkPaymentIntents");
        }
    }
}
