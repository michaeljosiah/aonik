using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialWebhookEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkFinancialWebhookEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinancialConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderConnectionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderEventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderEventCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcessingStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnkFinancialWebhookEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkFinancialWebhookEvents_AnkFinancialConnections_FinancialConnectionId",
                        column: x => x.FinancialConnectionId,
                        principalSchema: "dbo",
                        principalTable: "AnkFinancialConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialWebhookEvents_FinancialConnectionId",
                schema: "dbo",
                table: "AnkFinancialWebhookEvents",
                column: "FinancialConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialWebhookEvents_Provider_ProviderConnectionReference_ReceivedAt",
                schema: "dbo",
                table: "AnkFinancialWebhookEvents",
                columns: new[] { "Provider", "ProviderConnectionReference", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialWebhookEvents_Provider_ProviderEventType_ProviderEventCode_ReceivedAt",
                schema: "dbo",
                table: "AnkFinancialWebhookEvents",
                columns: new[] { "Provider", "ProviderEventType", "ProviderEventCode", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkFinancialWebhookEvents",
                schema: "dbo");
        }
    }
}
