using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPartyIdToExternalPayoutAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerPartyId",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AnkExternalPayoutAccounts_TenantId_CustomerPartyId_BeneficiaryPartyId",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts",
                columns: new[] { "TenantId", "CustomerPartyId", "BeneficiaryPartyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkExternalPayoutAccounts_TenantId_CustomerPartyId_BeneficiaryPartyId",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts");

            migrationBuilder.DropColumn(
                name: "CustomerPartyId",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts");
        }
    }
}
