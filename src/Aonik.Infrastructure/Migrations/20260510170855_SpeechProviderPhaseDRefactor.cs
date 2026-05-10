using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SpeechProviderPhaseDRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChainedSttLanguage",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChainedSttModel",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChainedTtsModelId",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChainedTtsVoiceId",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompositeInstructionsAddendum",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompositeModel",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompositeVoice",
                schema: "dbo",
                table: "AnkVoiceRecipes",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedApiKey",
                schema: "dbo",
                table: "AnkSpeechProviders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveTtsModelId",
                schema: "dbo",
                table: "AnkChatSpeechSettings",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveTtsVoiceId",
                schema: "dbo",
                table: "AnkChatSpeechSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkSpeechProviders_Tenant_Vendor_Type_Unique",
                schema: "dbo",
                table: "AnkSpeechProviders",
                columns: new[] { "TenantId", "Vendor", "Type" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkSpeechProviders_Tenant_Vendor_Type_Unique",
                schema: "dbo",
                table: "AnkSpeechProviders");

            migrationBuilder.DropColumn(
                name: "ChainedSttLanguage",
                schema: "dbo",
                table: "AnkVoiceRecipes");

            migrationBuilder.DropColumn(
                name: "ChainedSttModel",
                schema: "dbo",
                table: "AnkVoiceRecipes");

            migrationBuilder.DropColumn(
                name: "ChainedTtsModelId",
                schema: "dbo",
                table: "AnkVoiceRecipes");

            migrationBuilder.DropColumn(
                name: "ChainedTtsVoiceId",
                schema: "dbo",
                table: "AnkVoiceRecipes");

            migrationBuilder.DropColumn(
                name: "CompositeInstructionsAddendum",
                schema: "dbo",
                table: "AnkVoiceRecipes");

            migrationBuilder.DropColumn(
                name: "CompositeModel",
                schema: "dbo",
                table: "AnkVoiceRecipes");

            migrationBuilder.DropColumn(
                name: "CompositeVoice",
                schema: "dbo",
                table: "AnkVoiceRecipes");

            migrationBuilder.DropColumn(
                name: "EncryptedApiKey",
                schema: "dbo",
                table: "AnkSpeechProviders");

            migrationBuilder.DropColumn(
                name: "ActiveTtsModelId",
                schema: "dbo",
                table: "AnkChatSpeechSettings");

            migrationBuilder.DropColumn(
                name: "ActiveTtsVoiceId",
                schema: "dbo",
                table: "AnkChatSpeechSettings");
        }
    }
}
