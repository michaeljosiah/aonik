using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncCanonicalPreRegistrationChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnkCustomerInsightAiSummaries_AnkCustomerInsightAiSummaries_SupersededById",
                schema: "dbo",
                table: "AnkCustomerInsightAiSummaries");

            migrationBuilder.DropForeignKey(
                name: "FK_AnkCustomerInsightSnapshots_AnkCustomerInsightSnapshots_SupersededById",
                schema: "dbo",
                table: "AnkCustomerInsightSnapshots");

            migrationBuilder.CreateTable(
                name: "AnkPreRegistrationChallenges",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_AnkPreRegistrationChallenges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreRegistrationChallenge_Tenant_Phone",
                schema: "dbo",
                table: "AnkPreRegistrationChallenges",
                columns: new[] { "TenantId", "Phone" });

            migrationBuilder.AddForeignKey(
                name: "FK_AnkCustomerInsightAiSummaries_AnkCustomerInsightAiSummaries_SupersededById",
                schema: "dbo",
                table: "AnkCustomerInsightAiSummaries",
                column: "SupersededById",
                principalSchema: "dbo",
                principalTable: "AnkCustomerInsightAiSummaries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AnkCustomerInsightSnapshots_AnkCustomerInsightSnapshots_SupersededById",
                schema: "dbo",
                table: "AnkCustomerInsightSnapshots",
                column: "SupersededById",
                principalSchema: "dbo",
                principalTable: "AnkCustomerInsightSnapshots",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnkCustomerInsightAiSummaries_AnkCustomerInsightAiSummaries_SupersededById",
                schema: "dbo",
                table: "AnkCustomerInsightAiSummaries");

            migrationBuilder.DropForeignKey(
                name: "FK_AnkCustomerInsightSnapshots_AnkCustomerInsightSnapshots_SupersededById",
                schema: "dbo",
                table: "AnkCustomerInsightSnapshots");

            migrationBuilder.DropTable(
                name: "AnkPreRegistrationChallenges",
                schema: "dbo");

            migrationBuilder.AddForeignKey(
                name: "FK_AnkCustomerInsightAiSummaries_AnkCustomerInsightAiSummaries_SupersededById",
                schema: "dbo",
                table: "AnkCustomerInsightAiSummaries",
                column: "SupersededById",
                principalSchema: "dbo",
                principalTable: "AnkCustomerInsightAiSummaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AnkCustomerInsightSnapshots_AnkCustomerInsightSnapshots_SupersededById",
                schema: "dbo",
                table: "AnkCustomerInsightSnapshots",
                column: "SupersededById",
                principalSchema: "dbo",
                principalTable: "AnkCustomerInsightSnapshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
