using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFundingAccountEdgesToPersonalFinanceGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FundingAccountId",
                schema: "dbo",
                table: "AnkGoals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidFromAccountId",
                schema: "dbo",
                table: "AnkBills",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FundingAccountId",
                schema: "dbo",
                table: "AnkGoals");

            migrationBuilder.DropColumn(
                name: "PaidFromAccountId",
                schema: "dbo",
                table: "AnkBills");
        }
    }
}
