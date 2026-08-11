using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCeilingHoldings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkCeilingHoldings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriberKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeterCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Held = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkCeilingHoldings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkCeilingClaims",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CeilingHoldingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HolderRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AnkCeilingClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkCeilingClaims_AnkCeilingHoldings_CeilingHoldingId",
                        column: x => x.CeilingHoldingId,
                        principalSchema: "dbo",
                        principalTable: "AnkCeilingHoldings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkCeilingClaims_Holder_Unique",
                schema: "dbo",
                table: "AnkCeilingClaims",
                columns: new[] { "CeilingHoldingId", "HolderRef" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkCeilingHoldings_Subscriber_Unique",
                schema: "dbo",
                table: "AnkCeilingHoldings",
                columns: new[] { "TenantId", "SubscriberKind", "SubscriberId", "MeterCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkCeilingClaims",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkCeilingHoldings",
                schema: "dbo");
        }
    }
}
