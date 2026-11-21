using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectorMutationExecutionEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionJson",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvocationKey",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkToolApprovalRequests_TenantId_InvocationKey",
                schema: "dbo",
                table: "AnkToolApprovalRequests",
                columns: new[] { "TenantId", "InvocationKey" },
                unique: true,
                filter: "[InvocationKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkToolApprovalRequests_TenantId_InvocationKey",
                schema: "dbo",
                table: "AnkToolApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ExecutionJson",
                schema: "dbo",
                table: "AnkToolApprovalRequests");

            migrationBuilder.DropColumn(
                name: "InvocationKey",
                schema: "dbo",
                table: "AnkToolApprovalRequests");
        }
    }
}
