using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernedConnectorConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnkConnectorConnectionGrants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GranteeType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    GranteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowedActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkConnectorConnectionGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkConnectorConnectionRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectorKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DesiredCapability = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ownership = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiredScopesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AiRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActiveRequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    InputHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NextAction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReviewerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GrantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkflowGrantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OAuthStateHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProtectedVerifier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OAuthStartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OAuthExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OAuthLeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OAuthLeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProtectedOAuthResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsentedAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConsentedAccountLabel = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    ConsentedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkConnectorConnectionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkConnectorConnections",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectorKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Ownership = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProvisionedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ProviderAccountId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    ProviderAccountLabel = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    GrantedScopesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProtectedCredentials = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CredentialVersion = table.Column<int>(type: "int", nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RefreshLeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefreshLeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastHealthyAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
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
                    table.PrimaryKey("PK_AnkConnectorConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorConnectionGrants_TenantId_ConnectionId_GranteeType_GranteeId",
                schema: "dbo",
                table: "AnkConnectorConnectionGrants",
                columns: new[] { "TenantId", "ConnectionId", "GranteeType", "GranteeId" },
                unique: true,
                filter: "[RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorConnectionGrants_TenantId_GranteeType_GranteeId_RevokedAt",
                schema: "dbo",
                table: "AnkConnectorConnectionGrants",
                columns: new[] { "TenantId", "GranteeType", "GranteeId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorConnectionRequests_TenantId_ActiveRequestHash",
                schema: "dbo",
                table: "AnkConnectorConnectionRequests",
                columns: new[] { "TenantId", "ActiveRequestHash" },
                unique: true,
                filter: "[ActiveRequestHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorConnectionRequests_TenantId_RequesterUserId_IdempotencyKey",
                schema: "dbo",
                table: "AnkConnectorConnectionRequests",
                columns: new[] { "TenantId", "RequesterUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorConnectionRequests_TenantId_RequesterUserId_Status",
                schema: "dbo",
                table: "AnkConnectorConnectionRequests",
                columns: new[] { "TenantId", "RequesterUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorConnectionRequests_TenantId_ReviewerRoleId_Status",
                schema: "dbo",
                table: "AnkConnectorConnectionRequests",
                columns: new[] { "TenantId", "ReviewerRoleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorConnections_TenantId_ConnectorKey_Ownership_OwnerUserId",
                schema: "dbo",
                table: "AnkConnectorConnections",
                columns: new[] { "TenantId", "ConnectorKey", "Ownership", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorConnections_TenantId_ConnectorKey_Ownership_OwnerUserId_ProviderAccountId",
                schema: "dbo",
                table: "AnkConnectorConnections",
                columns: new[] { "TenantId", "ConnectorKey", "Ownership", "OwnerUserId", "ProviderAccountId" },
                unique: true,
                filter: "[ProviderAccountId] <> N'' AND [Status] <> N'Revoked'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnkConnectorConnectionGrants",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkConnectorConnectionRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkConnectorConnections",
                schema: "dbo");
        }
    }
}
