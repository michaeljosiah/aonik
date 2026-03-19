using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionTypeSubCategoryAndCategoryReferenceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubCategory",
                schema: "dbo",
                table: "AnkPersonalTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                schema: "dbo",
                table: "AnkPersonalTransactions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TransactionCategories",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IconName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TransactionCategories", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "TransactionCategories",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "DisplayName", "GroupName", "IconName", "IsActive", "IsDeleted", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("1187c214-6c2a-a5cb-eeda-c42742facb62"), "loan_payments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Loan Payments", "Financial", null, true, false, 50, null, null },
                    { new Guid("1997cca4-34be-9af3-771e-cf07004c7e55"), "transfer_in", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Transfer In", "Transfers", null, true, false, 2, null, null },
                    { new Guid("1bee5ee1-7f45-0bd0-5637-2f0a8016bc33"), "home_improvement", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Home Improvement", "Shopping", null, true, false, 21, null, null },
                    { new Guid("21c96e37-8556-81f9-5c95-f98c33dad96d"), "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Education", "Essentials", null, true, false, 14, null, null },
                    { new Guid("27dabf45-052c-8784-2620-6c70523d0f64"), "transportation", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Transportation", "Essentials", null, true, false, 12, null, null },
                    { new Guid("2ab4a16d-91d7-c1d0-25ff-69715bdecded"), "medical", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Medical", "Essentials", null, true, false, 13, null, null },
                    { new Guid("332c5fff-59e9-295b-bd0e-fe96d65e09c1"), "uncategorized", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Uncategorized", "Other", null, true, false, 99, null, null },
                    { new Guid("49de988e-ecea-4a92-bffc-95622e79be91"), "general_merchandise", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "General Merchandise", "Shopping", null, true, false, 20, null, null },
                    { new Guid("57f8c28b-6962-c2e3-291e-ce154606ddd9"), "income", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Income", "Income", null, true, false, 1, null, null },
                    { new Guid("65b2c4a6-9984-256c-91af-4bda390f02fb"), "personal_care", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Personal Care", "Shopping", null, true, false, 22, null, null },
                    { new Guid("70fd4eec-36ef-e460-ccf0-6361d3bb517b"), "general_services", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "General Services", "Services", null, true, false, 40, null, null },
                    { new Guid("73ed47bb-8061-8683-b87b-076e643f2da0"), "transfer_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Transfer Out", "Transfers", null, true, false, 3, null, null },
                    { new Guid("a0f43bd8-78f3-1b9c-a030-4d44f7df6cd1"), "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Bank Fees", "Financial", null, true, false, 51, null, null },
                    { new Guid("b4067e3f-8f67-675f-acbf-80778214a7c9"), "entertainment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Entertainment", "Lifestyle", null, true, false, 30, null, null },
                    { new Guid("b7981080-c87f-d631-1313-6a16c3b4f063"), "rent_and_utilities", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Rent & Utilities", "Essentials", null, true, false, 11, null, null },
                    { new Guid("cda09b4b-2680-24f9-273e-2379bb687a3b"), "food_and_drink", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Food & Drink", "Essentials", null, true, false, 10, null, null },
                    { new Guid("e334f27f-6a6a-bc10-9d2e-2110f5b372ae"), "travel", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Travel", "Lifestyle", null, true, false, 31, null, null },
                    { new Guid("e80f8dd4-d1a0-8d96-24f5-4954356423ac"), "government_and_non_profit", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Government & Non-Profit", "Services", null, true, false, 41, null, null },
                    { new Guid("f05ac5d2-08b7-bc07-2044-44f3ad4d6173"), "other", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Other", "Other", null, true, false, 90, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCategories_Code",
                schema: "dbo",
                table: "TransactionCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionCategories_SortOrder",
                schema: "dbo",
                table: "TransactionCategories",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionCategories",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "SubCategory",
                schema: "dbo",
                table: "AnkPersonalTransactions");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                schema: "dbo",
                table: "AnkPersonalTransactions");
        }
    }
}
