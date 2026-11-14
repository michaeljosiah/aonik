using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowGovernanceReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviewJson",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionResultJson",
                schema: "dbo",
                table: "AnkProposals",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewJson",
                schema: "dbo",
                table: "AnkToolApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ExecutionResultJson",
                schema: "dbo",
                table: "AnkProposals");
        }
    }
}
