using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireBillerPartnerAndAddPartnerFundingAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @utcNow datetime2 = SYSUTCDATETIME();

                ;WITH TenantNeedingPlaceholder AS (
                    SELECT DISTINCT cb.TenantId
                    FROM CatalogBillers cb
                    WHERE cb.CorrespondentPartnerId IS NULL
                )
                INSERT INTO Partners (
                    Id,
                    TenantId,
                    Name,
                    Status,
                    CapabilitiesJson,
                    OperatingHoursJson,
                    CreatedAt,
                    CreatedBy,
                    UpdatedAt,
                    UpdatedBy,
                    RowVersion,
                    IsDeleted,
                    DeletedAt,
                    DeletedBy)
                SELECT
                    NEWID(),
                    tenant.TenantId,
                    N'Unassigned Catalog Partner',
                    N'Active',
                    N'[]',
                    N'{}',
                    @utcNow,
                    NULL,
                    NULL,
                    NULL,
                    0x00,
                    0,
                    NULL,
                    NULL
                FROM TenantNeedingPlaceholder tenant
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM Partners partner
                    WHERE partner.TenantId = tenant.TenantId
                      AND partner.Name = N'Unassigned Catalog Partner');

                UPDATE cb
                SET CorrespondentPartnerId = partner.Id
                FROM CatalogBillers cb
                CROSS APPLY (
                    SELECT TOP 1 p.Id
                    FROM Partners p
                    WHERE p.TenantId = cb.TenantId
                    ORDER BY
                        CASE WHEN p.Name = N'Unassigned Catalog Partner' THEN 0 ELSE 1 END,
                        p.CreatedAt,
                        p.Id)
                    partner
                WHERE cb.CorrespondentPartnerId IS NULL;

                IF EXISTS (
                    SELECT 1
                    FROM CatalogBillers
                    WHERE CorrespondentPartnerId IS NULL)
                BEGIN
                    THROW 50000, 'Cannot migrate CatalogBillers: unresolved null CorrespondentPartnerId rows remain.', 1;
                END
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CorrespondentPartnerId",
                table: "CatalogBillers",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "PartnerFundingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LedgerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AccountRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerFundingAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerFundingAccounts_LedgerAccounts_LedgerAccountId",
                        column: x => x.LedgerAccountId,
                        principalTable: "LedgerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartnerFundingAccounts_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBillers_CorrespondentPartnerId",
                table: "CatalogBillers",
                column: "CorrespondentPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerFundingAccounts_LedgerAccountId",
                table: "PartnerFundingAccounts",
                column: "LedgerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerFundingAccounts_PartnerId",
                table: "PartnerFundingAccounts",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerFundingAccounts_TenantId_LedgerAccountId",
                table: "PartnerFundingAccounts",
                columns: new[] { "TenantId", "LedgerAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerFundingAccounts_TenantId_PartnerId_Currency_AccountRole",
                table: "PartnerFundingAccounts",
                columns: new[] { "TenantId", "PartnerId", "Currency", "AccountRole" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogBillers_Partners_CorrespondentPartnerId",
                table: "CatalogBillers",
                column: "CorrespondentPartnerId",
                principalTable: "Partners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatalogBillers_Partners_CorrespondentPartnerId",
                table: "CatalogBillers");

            migrationBuilder.DropTable(
                name: "PartnerFundingAccounts");

            migrationBuilder.DropIndex(
                name: "IX_CatalogBillers_CorrespondentPartnerId",
                table: "CatalogBillers");

            migrationBuilder.AlterColumn<Guid>(
                name: "CorrespondentPartnerId",
                table: "CatalogBillers",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }
    }
}
