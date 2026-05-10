using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceRecipeLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkVoiceRecipes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChainedSttProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ChainedTtsProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ChainedPinnedAgentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ChainedVad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ChainedVadStopMs = table.Column<int>(type: "int", nullable: true),
                    ChainedTranscriptionFilter = table.Column<bool>(type: "bit", nullable: true),
                    ChainedSentenceAggregator = table.Column<bool>(type: "bit", nullable: true),
                    CompositeProviderId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CompositePinnedAgentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PreviousVersionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_AnkVoiceRecipes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkVoiceRecipes_Tenant_ChainedSttProviderId",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                columns: new[] { "TenantId", "ChainedSttProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkVoiceRecipes_Tenant_ChainedTtsProviderId",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                columns: new[] { "TenantId", "ChainedTtsProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkVoiceRecipes_Tenant_CompositeProviderId",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                columns: new[] { "TenantId", "CompositeProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkVoiceRecipes_Tenant_Kind_IsDeleted",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                columns: new[] { "TenantId", "Kind", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkVoiceRecipes",
                schema: "dbo");
        }
    }
}
