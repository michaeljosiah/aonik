using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerConnectorCredentialBundles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PartnerWebhookEvents_ProviderCode_PayloadHash",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents");

            migrationBuilder.DropIndex(
                name: "UX_PartnerWebhookEvents_ProviderCode_ProviderEventId",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents");

            migrationBuilder.AddColumn<Guid>(
                name: "ConnectorId",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ConnectorType",
                schema: "dbo",
                table: "AnkConnectors",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "IsLegacyDefault",
                schema: "dbo",
                table: "AnkConnectors",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AnkCredentialBundles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ref = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConnectorKind = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProtectedSecretsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FieldMetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_AnkCredentialBundles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPartnerWebhookEvents_ConnectorId",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents",
                column: "ConnectorId");

            migrationBuilder.CreateIndex(
                name: "UX_PartnerWebhookEvents_Connector_PayloadHash",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents",
                columns: new[] { "ConnectorId", "PayloadHash" },
                unique: true,
                filter: "[ConnectorId] IS NOT NULL AND [ProviderEventId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PartnerWebhookEvents_Connector_ProviderEventId",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents",
                columns: new[] { "ConnectorId", "ProviderEventId" },
                unique: true,
                filter: "[ConnectorId] IS NOT NULL AND [ProviderEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PartnerWebhookEvents_ProviderCode_PayloadHash",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents",
                columns: new[] { "ProviderCode", "PayloadHash" },
                unique: true,
                filter: "[ConnectorId] IS NULL AND [ProviderEventId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PartnerWebhookEvents_ProviderCode_ProviderEventId",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents",
                columns: new[] { "ProviderCode", "ProviderEventId" },
                unique: true,
                filter: "[ConnectorId] IS NULL AND [ProviderEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Connectors_TenantId_ConnectorType_LegacyDefault",
                schema: "dbo",
                table: "AnkConnectors",
                columns: new[] { "TenantId", "ConnectorType" },
                unique: true,
                filter: "[IsLegacyDefault] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_CredentialBundles_TenantId_Ref",
                schema: "dbo",
                table: "AnkCredentialBundles",
                columns: new[] { "TenantId", "Ref" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkCredentialBundles",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AnkPartnerWebhookEvents_ConnectorId",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents");

            migrationBuilder.DropIndex(
                name: "UX_PartnerWebhookEvents_Connector_PayloadHash",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents");

            migrationBuilder.DropIndex(
                name: "UX_PartnerWebhookEvents_Connector_ProviderEventId",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents");

            migrationBuilder.DropIndex(
                name: "UX_PartnerWebhookEvents_ProviderCode_PayloadHash",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents");

            migrationBuilder.DropIndex(
                name: "UX_PartnerWebhookEvents_ProviderCode_ProviderEventId",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents");

            migrationBuilder.DropIndex(
                name: "UX_Connectors_TenantId_ConnectorType_LegacyDefault",
                schema: "dbo",
                table: "AnkConnectors");

            migrationBuilder.DropColumn(
                name: "ConnectorId",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents");

            migrationBuilder.DropColumn(
                name: "IsLegacyDefault",
                schema: "dbo",
                table: "AnkConnectors");

            migrationBuilder.AlterColumn<string>(
                name: "ConnectorType",
                schema: "dbo",
                table: "AnkConnectors",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "UX_PartnerWebhookEvents_ProviderCode_PayloadHash",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents",
                columns: new[] { "ProviderCode", "PayloadHash" },
                unique: true,
                filter: "[ProviderEventId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PartnerWebhookEvents_ProviderCode_ProviderEventId",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents",
                columns: new[] { "ProviderCode", "ProviderEventId" },
                unique: true,
                filter: "[ProviderEventId] IS NOT NULL");
        }
    }
}
