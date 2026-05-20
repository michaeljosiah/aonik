using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTransactionCategorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CategoryConfidence",
                schema: "dbo",
                table: "AccountTransactions",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CategoryLockedAt",
                schema: "dbo",
                table: "AccountTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryMethod",
                schema: "dbo",
                table: "AccountTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubCategory",
                schema: "dbo",
                table: "AccountTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountTransactionMerchantCategories",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_AccountTransactionMerchantCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactionMerchantCategories_TenantId_MerchantKey",
                schema: "dbo",
                table: "AccountTransactionMerchantCategories",
                columns: new[] { "TenantId", "MerchantKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountTransactionMerchantCategories",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "CategoryConfidence",
                schema: "dbo",
                table: "AccountTransactions");

            migrationBuilder.DropColumn(
                name: "CategoryLockedAt",
                schema: "dbo",
                table: "AccountTransactions");

            migrationBuilder.DropColumn(
                name: "CategoryMethod",
                schema: "dbo",
                table: "AccountTransactions");

            migrationBuilder.DropColumn(
                name: "SubCategory",
                schema: "dbo",
                table: "AccountTransactions");
        }
    }
}
