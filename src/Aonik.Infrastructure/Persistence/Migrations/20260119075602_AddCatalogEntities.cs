using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AonikBackgroundJobRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArgumentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    MaxRetryCount = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TryCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ErrorDetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AonikBackgroundJobRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogBillerCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IconUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_CatalogBillerCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogBillers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrespondentPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BannerUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupportPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SupportEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_CatalogBillers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogBillerServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    MinAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    SupportsPartialPayment = table.Column<bool>(type: "bit", nullable: false),
                    RequiresValidation = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValidationJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_CatalogBillerServices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobRecords_NextAttemptAt",
                table: "AonikBackgroundJobRecords",
                column: "NextAttemptAt");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobRecords_Priority",
                table: "AonikBackgroundJobRecords",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobRecords_Status",
                table: "AonikBackgroundJobRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundJobRecords_TenantId",
                table: "AonikBackgroundJobRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBillerCategories_TenantId_CountryCode_Name",
                table: "CatalogBillerCategories",
                columns: new[] { "TenantId", "CountryCode", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBillerCategories_TenantId_CountryCode_SortOrder",
                table: "CatalogBillerCategories",
                columns: new[] { "TenantId", "CountryCode", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBillers_TenantId_CorrespondentPartnerId",
                table: "CatalogBillers",
                columns: new[] { "TenantId", "CorrespondentPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBillers_TenantId_CountryCode_CategoryId_SortOrder",
                table: "CatalogBillers",
                columns: new[] { "TenantId", "CountryCode", "CategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBillers_TenantId_CountryCode_Name",
                table: "CatalogBillers",
                columns: new[] { "TenantId", "CountryCode", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBillerServices_TenantId_BillerId_Name",
                table: "CatalogBillerServices",
                columns: new[] { "TenantId", "BillerId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBillerServices_TenantId_BillerId_SortOrder",
                table: "CatalogBillerServices",
                columns: new[] { "TenantId", "BillerId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AonikBackgroundJobRecords");

            migrationBuilder.DropTable(
                name: "CatalogBillerCategories");

            migrationBuilder.DropTable(
                name: "CatalogBillers");

            migrationBuilder.DropTable(
                name: "CatalogBillerServices");
        }
    }
}
