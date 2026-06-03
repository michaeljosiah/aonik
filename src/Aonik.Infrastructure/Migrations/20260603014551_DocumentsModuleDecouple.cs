using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentsModuleDecouple : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnkDocumentUsages_AnkDocuments_DocumentId",
                schema: "dbo",
                table: "AnkDocumentUsages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_AnkDocumentUsages_AnkDocuments_DocumentId",
                schema: "dbo",
                table: "AnkDocumentUsages",
                column: "DocumentId",
                principalSchema: "dbo",
                principalTable: "AnkDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
