using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentsModulePhase1Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Classification",
                schema: "dbo",
                table: "AnkDocuments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "IndexStatus",
                schema: "dbo",
                table: "AnkDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "IndexedAt",
                schema: "dbo",
                table: "AnkDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "dbo",
                table: "AnkDocuments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "AdminUpload");

            migrationBuilder.AddColumn<int>(
                name: "ExtractedTextStatus",
                schema: "dbo",
                table: "AnkDocumentFiles",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "AnkDocumentExtractions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExtractionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    Confidence = table.Column<double>(type: "float", nullable: true),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkDocumentExtractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkDocumentIngestions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentFileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VectorCollection = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChunkCount = table.Column<int>(type: "int", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmbeddingCost = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkDocumentIngestions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkDocumentExtractions_DocumentId",
                schema: "dbo",
                table: "AnkDocumentExtractions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkDocumentIngestions_DocumentFileId",
                schema: "dbo",
                table: "AnkDocumentIngestions",
                column: "DocumentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkDocumentIngestions_DocumentId",
                schema: "dbo",
                table: "AnkDocumentIngestions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkDocumentIngestions_Status",
                schema: "dbo",
                table: "AnkDocumentIngestions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkDocumentExtractions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkDocumentIngestions",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "Classification",
                schema: "dbo",
                table: "AnkDocuments");

            migrationBuilder.DropColumn(
                name: "IndexStatus",
                schema: "dbo",
                table: "AnkDocuments");

            migrationBuilder.DropColumn(
                name: "IndexedAt",
                schema: "dbo",
                table: "AnkDocuments");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "dbo",
                table: "AnkDocuments");

            migrationBuilder.DropColumn(
                name: "ExtractedTextStatus",
                schema: "dbo",
                table: "AnkDocumentFiles");
        }
    }
}
