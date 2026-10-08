using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignupSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkSignupSubscriptions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Postcode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    PostcodeOutwardCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Service = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ConsentVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConsentTextSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ConsentSource = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConsentedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnsubscribedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AnkSignupSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkSignupSubscriptions_TenantId_ListType_NormalizedEmail",
                schema: "dbo",
                table: "AnkSignupSubscriptions",
                columns: new[] { "TenantId", "ListType", "NormalizedEmail" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkSignupSubscriptions_TenantId_ListType_UnsubscribedAtUtc",
                schema: "dbo",
                table: "AnkSignupSubscriptions",
                columns: new[] { "TenantId", "ListType", "UnsubscribedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkSignupSubscriptions",
                schema: "dbo");
        }
    }
}
