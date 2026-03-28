using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualExternalAccountCrud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalAccountTransactions_ExternalAccountConnections_ExternalAccountConnectionId",
                schema: "dbo",
                table: "ExternalAccountTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ExternalAccountTransactions_TenantId_ExternalAccountConnectionId_ProviderTransactionReference",
                schema: "dbo",
                table: "ExternalAccountTransactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ExternalAccountConnectionId",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateTable(
                name: "ExternalAccountTransactionAttachments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StorageContainer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_ExternalAccountTransactionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalAccountTransactionAttachments_ExternalAccountTransactions_TransactionId",
                        column: x => x.TransactionId,
                        principalSchema: "dbo",
                        principalTable: "ExternalAccountTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountTransactions_TenantId_ExternalAccountConnectionId_ProviderTransactionReference",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                columns: new[] { "TenantId", "ExternalAccountConnectionId", "ProviderTransactionReference" },
                unique: true,
                filter: "[ExternalAccountConnectionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountTransactions_TenantId_ExternalAccountId_ProviderTransactionReference",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                columns: new[] { "TenantId", "ExternalAccountId", "ProviderTransactionReference" },
                unique: true,
                filter: "[ExternalAccountConnectionId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountTransactionAttachments_TenantId_TransactionId",
                schema: "dbo",
                table: "ExternalAccountTransactionAttachments",
                columns: new[] { "TenantId", "TransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountTransactionAttachments_TransactionId",
                schema: "dbo",
                table: "ExternalAccountTransactionAttachments",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalAccountTransactions_ExternalAccountConnections_ExternalAccountConnectionId",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                column: "ExternalAccountConnectionId",
                principalSchema: "dbo",
                principalTable: "ExternalAccountConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalAccountTransactions_ExternalAccountConnections_ExternalAccountConnectionId",
                schema: "dbo",
                table: "ExternalAccountTransactions");

            migrationBuilder.DropTable(
                name: "ExternalAccountTransactionAttachments",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_ExternalAccountTransactions_TenantId_ExternalAccountConnectionId_ProviderTransactionReference",
                schema: "dbo",
                table: "ExternalAccountTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ExternalAccountTransactions_TenantId_ExternalAccountId_ProviderTransactionReference",
                schema: "dbo",
                table: "ExternalAccountTransactions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ExternalAccountConnectionId",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountTransactions_TenantId_ExternalAccountConnectionId_ProviderTransactionReference",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                columns: new[] { "TenantId", "ExternalAccountConnectionId", "ProviderTransactionReference" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalAccountTransactions_ExternalAccountConnections_ExternalAccountConnectionId",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                column: "ExternalAccountConnectionId",
                principalSchema: "dbo",
                principalTable: "ExternalAccountConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
