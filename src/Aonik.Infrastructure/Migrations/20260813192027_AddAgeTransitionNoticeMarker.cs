using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgeTransitionNoticeMarker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AgeTransitionNoticeSentOn",
                schema: "dbo",
                table: "AnkParties",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgeTransitionNoticeSentOn",
                schema: "dbo",
                table: "AnkParties");
        }
    }
}
