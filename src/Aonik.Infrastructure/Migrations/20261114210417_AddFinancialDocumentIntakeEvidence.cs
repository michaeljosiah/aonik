using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialDocumentIntakeEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntakeBusinessKey",
                schema: "dbo",
                table: "AnkInvoices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                schema: "dbo",
                table: "AnkInvoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceEvidenceJson",
                schema: "dbo",
                table: "AnkDocumentFiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFingerprint",
                schema: "dbo",
                table: "AnkDocumentFiles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentFileId",
                schema: "dbo",
                table: "AnkDocumentExtractions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InputHash",
                schema: "dbo",
                table: "AnkDocumentExtractions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkDocumentExtractions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Completed");

            migrationBuilder.CreateIndex(
                name: "IX_AnkInvoices_TenantId_IntakeBusinessKey",
                schema: "dbo",
                table: "AnkInvoices",
                columns: new[] { "TenantId", "IntakeBusinessKey" },
                unique: true,
                filter: "[IntakeBusinessKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkDocumentFiles_TenantId_SourceFingerprint",
                schema: "dbo",
                table: "AnkDocumentFiles",
                columns: new[] { "TenantId", "SourceFingerprint" },
                unique: true,
                filter: "[SourceFingerprint] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkDocumentExtractions_TenantId_DocumentId_ExtractionType_InputHash",
                schema: "dbo",
                table: "AnkDocumentExtractions",
                columns: new[] { "TenantId", "DocumentId", "ExtractionType", "InputHash" },
                unique: true,
                filter: "[InputHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnkInvoices_TenantId_IntakeBusinessKey",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropIndex(
                name: "IX_AnkDocumentFiles_TenantId_SourceFingerprint",
                schema: "dbo",
                table: "AnkDocumentFiles");

            migrationBuilder.DropIndex(
                name: "IX_AnkDocumentExtractions_TenantId_DocumentId_ExtractionType_InputHash",
                schema: "dbo",
                table: "AnkDocumentExtractions");

            migrationBuilder.DropColumn(
                name: "IntakeBusinessKey",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                schema: "dbo",
                table: "AnkInvoices");

            migrationBuilder.DropColumn(
                name: "SourceEvidenceJson",
                schema: "dbo",
                table: "AnkDocumentFiles");

            migrationBuilder.DropColumn(
                name: "SourceFingerprint",
                schema: "dbo",
                table: "AnkDocumentFiles");

            migrationBuilder.DropColumn(
                name: "DocumentFileId",
                schema: "dbo",
                table: "AnkDocumentExtractions");

            migrationBuilder.DropColumn(
                name: "InputHash",
                schema: "dbo",
                table: "AnkDocumentExtractions");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "dbo",
                table: "AnkDocumentExtractions");
        }
    }
}
