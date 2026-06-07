using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedPayoutBeneficiaryRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderCode",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RailFingerprint",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkExternalPayoutAccounts_TenantId_CustomerPartyId_ProviderCode_RailFingerprint",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts",
                columns: new[] { "TenantId", "CustomerPartyId", "ProviderCode", "RailFingerprint" },
                unique: true,
                filter: "[ProviderCode] IS NOT NULL AND [RailFingerprint] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkExternalPayoutAccounts_TenantId_CustomerPartyId_ProviderCode_RailFingerprint",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts");

            migrationBuilder.DropColumn(
                name: "ProviderCode",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts");

            migrationBuilder.DropColumn(
                name: "RailFingerprint",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts");
        }
    }
}
