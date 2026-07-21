using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceProductOptionGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitSurcharge",
                schema: "dbo",
                table: "AnkProducts",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitSurchargeCurrency",
                schema: "dbo",
                table: "AnkProducts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkOptionGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    HelpText = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SelectionMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_AnkOptionGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkProductOptionGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowedChoiceKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DefaultChoiceKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SelectionModeOverride = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AnkProductOptionGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkOptionChoices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IsRecommendedDefault = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnkOptionChoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkOptionChoices_AnkOptionGroups_OptionGroupId",
                        column: x => x.OptionGroupId,
                        principalSchema: "dbo",
                        principalTable: "AnkOptionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkOptionChoices_OptionGroupId",
                schema: "dbo",
                table: "AnkOptionChoices",
                column: "OptionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOptionChoices_RecommendedDefault_Unique",
                schema: "dbo",
                table: "AnkOptionChoices",
                columns: new[] { "TenantId", "OptionGroupId" },
                unique: true,
                filter: "[IsRecommendedDefault] = 1 AND [IsActive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOptionChoices_TenantId_OptionGroupId_Key",
                schema: "dbo",
                table: "AnkOptionChoices",
                columns: new[] { "TenantId", "OptionGroupId", "Key" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOptionGroups_TenantId_Key",
                schema: "dbo",
                table: "AnkOptionGroups",
                columns: new[] { "TenantId", "Key" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkProductOptionGroups_TenantId_ProductId",
                schema: "dbo",
                table: "AnkProductOptionGroups",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkProductOptionGroups_TenantId_ProductId_OptionGroupId",
                schema: "dbo",
                table: "AnkProductOptionGroups",
                columns: new[] { "TenantId", "ProductId", "OptionGroupId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkOptionChoices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkProductOptionGroups",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkOptionGroups",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "UnitSurcharge",
                schema: "dbo",
                table: "AnkProducts");

            migrationBuilder.DropColumn(
                name: "UnitSurchargeCurrency",
                schema: "dbo",
                table: "AnkProducts");
        }
    }
}
