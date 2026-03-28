using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalAccountLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalAccountConnections",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderConnectionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InstitutionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InstitutionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AutoSyncEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SyncIntervalMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 360),
                    NextScheduledSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastWebhookReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConsentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SecretReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SyncCursor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisconnectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ExternalAccountConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalAccountConnectionSessions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalAccountConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SessionToken = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProviderSessionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ExternalAccountConnectionSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalAccountLinkedAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalAccountConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderAccountReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountSubtype = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Last4 = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ExternalAccountLinkedAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalAccountLinkedAccounts_ExternalAccountConnections_ExternalAccountConnectionId",
                        column: x => x.ExternalAccountConnectionId,
                        principalSchema: "dbo",
                        principalTable: "ExternalAccountConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalAccountTransactions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalAccountConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderTransactionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Counterparty = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Pending = table.Column<bool>(type: "bit", nullable: false),
                    ReconciliationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MatchedLedgerEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedPayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReconciledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconciledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_ExternalAccountTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalAccountTransactions_ExternalAccountConnections_ExternalAccountConnectionId",
                        column: x => x.ExternalAccountConnectionId,
                        principalSchema: "dbo",
                        principalTable: "ExternalAccountConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountConnections_AutoSyncEnabled_NextScheduledSyncAt",
                schema: "dbo",
                table: "ExternalAccountConnections",
                columns: new[] { "AutoSyncEnabled", "NextScheduledSyncAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountConnections_TenantId_Provider_ProviderConnectionReference",
                schema: "dbo",
                table: "ExternalAccountConnections",
                columns: new[] { "TenantId", "Provider", "ProviderConnectionReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountConnections_TenantId_Status",
                schema: "dbo",
                table: "ExternalAccountConnections",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountConnectionSessions_ExternalAccountConnectionId",
                schema: "dbo",
                table: "ExternalAccountConnectionSessions",
                column: "ExternalAccountConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountConnectionSessions_SessionToken",
                schema: "dbo",
                table: "ExternalAccountConnectionSessions",
                column: "SessionToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountConnectionSessions_TenantId_UserId_Provider_Status",
                schema: "dbo",
                table: "ExternalAccountConnectionSessions",
                columns: new[] { "TenantId", "UserId", "Provider", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountLinkedAccounts_ExternalAccountConnectionId",
                schema: "dbo",
                table: "ExternalAccountLinkedAccounts",
                column: "ExternalAccountConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountLinkedAccounts_TenantId_ExternalAccountConnectionId_ProviderAccountReference",
                schema: "dbo",
                table: "ExternalAccountLinkedAccounts",
                columns: new[] { "TenantId", "ExternalAccountConnectionId", "ProviderAccountReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountLinkedAccounts_TenantId_ExternalAccountId",
                schema: "dbo",
                table: "ExternalAccountLinkedAccounts",
                columns: new[] { "TenantId", "ExternalAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountTransactions_ExternalAccountConnectionId",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                column: "ExternalAccountConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountTransactions_TenantId_ExternalAccountConnectionId_ProviderTransactionReference",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                columns: new[] { "TenantId", "ExternalAccountConnectionId", "ProviderTransactionReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountTransactions_TenantId_ExternalAccountId_OccurredAt",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                columns: new[] { "TenantId", "ExternalAccountId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAccountTransactions_TenantId_ReconciliationStatus",
                schema: "dbo",
                table: "ExternalAccountTransactions",
                columns: new[] { "TenantId", "ReconciliationStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalAccountConnectionSessions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ExternalAccountLinkedAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ExternalAccountTransactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ExternalAccountConnections",
                schema: "dbo");
        }
    }
}
