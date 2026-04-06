using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkAiTasks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UseCase = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExecutionMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PromptName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SystemTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeveloperTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VariablesSchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputSchemaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnkAiTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkAiTasks_PromptName_PromptVersion_TenantId",
                schema: "dbo",
                table: "AnkAiTasks",
                columns: new[] { "PromptName", "PromptVersion", "TenantId" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnkAiTasks_UseCase_TenantId",
                schema: "dbo",
                table: "AnkAiTasks",
                columns: new[] { "UseCase", "TenantId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkAiTasks",
                schema: "dbo");
        }
    }
}
