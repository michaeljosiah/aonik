using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationAndMarketingPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkMarketingPreferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    News = table.Column<bool>(type: "bit", nullable: false),
                    Offers = table.Column<bool>(type: "bit", nullable: false),
                    Surveys = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnkMarketingPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkMarketingPreferences_AnkParties_PartyId",
                        column: x => x.PartyId,
                        principalSchema: "dbo",
                        principalTable: "AnkParties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnkNotificationPreferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    NewBillsPush = table.Column<bool>(type: "bit", nullable: false),
                    BillUpdatesPush = table.Column<bool>(type: "bit", nullable: false),
                    BillAssistPush = table.Column<bool>(type: "bit", nullable: false),
                    MbaMessagesPush = table.Column<bool>(type: "bit", nullable: false),
                    OrgMessagesPush = table.Column<bool>(type: "bit", nullable: false),
                    FriendsMessagesPush = table.Column<bool>(type: "bit", nullable: false),
                    NewBillsEmail = table.Column<bool>(type: "bit", nullable: false),
                    BillUpdatesEmail = table.Column<bool>(type: "bit", nullable: false),
                    BillAssistEmail = table.Column<bool>(type: "bit", nullable: false),
                    MbaMessagesEmail = table.Column<bool>(type: "bit", nullable: false),
                    OrgMessagesEmail = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AnkNotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkNotificationPreferences_AnkParties_PartyId",
                        column: x => x.PartyId,
                        principalSchema: "dbo",
                        principalTable: "AnkParties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketingPreference_PartyId",
                schema: "dbo",
                table: "AnkMarketingPreferences",
                column: "PartyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreference_PartyId",
                schema: "dbo",
                table: "AnkNotificationPreferences",
                column: "PartyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkMarketingPreferences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkNotificationPreferences",
                schema: "dbo");
        }
    }
}
