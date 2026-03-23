using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalAiModelCatalogKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AnkAiModels_AiProviderId' AND object_id = OBJECT_ID('[dbo].[AnkAiModels]')) DROP INDEX [IX_AnkAiModels_AiProviderId] ON [dbo].[AnkAiModels];");

            migrationBuilder.AddColumn<string>(
                name: "ExternalModelProviderKey",
                schema: "dbo",
                table: "AnkAiProviders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalModelKey",
                schema: "dbo",
                table: "AnkAiModels",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkAiProviders_ExternalModelProviderKey",
                schema: "dbo",
                table: "AnkAiProviders",
                column: "ExternalModelProviderKey",
                unique: true,
                filter: "[ExternalModelProviderKey] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkAiModels_AiProviderId_ExternalModelKey",
                schema: "dbo",
                table: "AnkAiModels",
                columns: new[] { "AiProviderId", "ExternalModelKey" },
                unique: true,
                filter: "[ExternalModelKey] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkAiProviders_ExternalModelProviderKey",
                schema: "dbo",
                table: "AnkAiProviders");

            migrationBuilder.DropIndex(
                name: "IX_AnkAiModels_AiProviderId_ExternalModelKey",
                schema: "dbo",
                table: "AnkAiModels");

            migrationBuilder.DropColumn(
                name: "ExternalModelProviderKey",
                schema: "dbo",
                table: "AnkAiProviders");

            migrationBuilder.DropColumn(
                name: "ExternalModelKey",
                schema: "dbo",
                table: "AnkAiModels");

            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AnkAiModels_AiProviderId' AND object_id = OBJECT_ID('[dbo].[AnkAiModels]')) CREATE INDEX [IX_AnkAiModels_AiProviderId] ON [dbo].[AnkAiModels] ([AiProviderId]);");
        }
    }
}
