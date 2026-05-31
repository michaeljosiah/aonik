using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartnerIntegrationAbstraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "PayoutId",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentIntentId",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReference",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawResponseJson",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PaymentId",
                schema: "dbo",
                table: "AnkRefunds",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "dbo",
                table: "AnkRefunds",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "ClientReference",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConnectorId",
                schema: "dbo",
                table: "AnkRefunds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentIntentId",
                schema: "dbo",
                table: "AnkRefunds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReference",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawResponseJson",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "dbo",
                table: "AnkPayouts",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "ClientReference",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ConnectorId",
                schema: "dbo",
                table: "AnkPayouts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "ConvertedAmount",
                schema: "dbo",
                table: "AnkPayouts",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DebitCurrency",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DestinationType",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fee",
                schema: "dbo",
                table: "AnkPayouts",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeeCurrency",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FxRate",
                schema: "dbo",
                table: "AnkPayouts",
                type: "decimal(19,8)",
                precision: 19,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Narration",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderItemId",
                schema: "dbo",
                table: "AnkPayouts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReference",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RawResponseJson",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientReference",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionMethod",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionStatus",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConnectorId",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fee",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FxRate",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "decimal(19,8)",
                precision: 19,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaskedPhoneNumber",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobileNetwork",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextActionMode",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextActionRedirectUrl",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextActionUssdCode",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReference",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SettledAmount",
                schema: "dbo",
                table: "AnkPaymentIntents",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PayoutId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmountType",
                schema: "dbo",
                table: "AnkCatalogBillerServices",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "FixedAmount",
                schema: "dbo",
                table: "AnkCatalogBillerServices",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnkBillValidations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConnectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogBillerServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValidationToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ResolvedFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_AnkBillValidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkBillValidations_AnkCatalogBillerServices_CatalogBillerServiceId",
                        column: x => x.CatalogBillerServiceId,
                        principalSchema: "dbo",
                        principalTable: "AnkCatalogBillerServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnkBillValidations_AnkConnectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalSchema: "dbo",
                        principalTable: "AnkConnectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkConnectorBillerMappings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogBillerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogBillerServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConnectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderBillerCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_AnkConnectorBillerMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkConnectorBillerMappings_AnkCatalogBillerServices_CatalogBillerServiceId",
                        column: x => x.CatalogBillerServiceId,
                        principalSchema: "dbo",
                        principalTable: "AnkCatalogBillerServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnkConnectorBillerMappings_AnkCatalogBillers_CatalogBillerId",
                        column: x => x.CatalogBillerId,
                        principalSchema: "dbo",
                        principalTable: "AnkCatalogBillers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnkConnectorBillerMappings_AnkConnectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalSchema: "dbo",
                        principalTable: "AnkConnectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkConnectorCapabilities",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_AnkConnectorCapabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkConnectorCapabilities_AnkConnectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalSchema: "dbo",
                        principalTable: "AnkConnectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkExternalPayoutAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeneficiaryPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConnectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DestinationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    BranchCode = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    MobileNetwork = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaskedAccountIdentifier = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProviderBeneficiaryId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VaultRef = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_AnkExternalPayoutAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkExternalPayoutAccounts_AnkConnectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalSchema: "dbo",
                        principalTable: "AnkConnectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnkExternalPayoutAccounts_AnkPartners_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "dbo",
                        principalTable: "AnkPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkFinancialInstitutions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InstitutionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Bic = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
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
                    table.PrimaryKey("PK_AnkFinancialInstitutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkPartnerWebhookEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProviderCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignatureValid = table.Column<bool>(type: "bit", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AnkPartnerWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnkPayoutReversals",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AnkPayoutReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkPayoutReversals_AnkPayouts_PayoutId",
                        column: x => x.PayoutId,
                        principalSchema: "dbo",
                        principalTable: "AnkPayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkPartnerBillPayments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConnectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectorBillerMappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BillerCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ClientReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BillValidationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VendToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RawResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_AnkPartnerBillPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkPartnerBillPayments_AnkBillValidations_BillValidationId",
                        column: x => x.BillValidationId,
                        principalSchema: "dbo",
                        principalTable: "AnkBillValidations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnkPartnerBillPayments_AnkConnectorBillerMappings_ConnectorBillerMappingId",
                        column: x => x.ConnectorBillerMappingId,
                        principalSchema: "dbo",
                        principalTable: "AnkConnectorBillerMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnkPartnerBillPayments_AnkConnectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalSchema: "dbo",
                        principalTable: "AnkConnectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnkConnectorInstitutionCodes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinancialInstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderInstitutionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_AnkConnectorInstitutionCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnkConnectorInstitutionCodes_AnkConnectors_ConnectorId",
                        column: x => x.ConnectorId,
                        principalSchema: "dbo",
                        principalTable: "AnkConnectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnkConnectorInstitutionCodes_AnkFinancialInstitutions_FinancialInstitutionId",
                        column: x => x.FinancialInstitutionId,
                        principalSchema: "dbo",
                        principalTable: "AnkFinancialInstitutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnkTransmissions_PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkTransmissions",
                column: "PartnerBillPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkTransmissions_PaymentIntentId",
                schema: "dbo",
                table: "AnkTransmissions",
                column: "PaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkTransmissions_PayoutId",
                schema: "dbo",
                table: "AnkTransmissions",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkTransmissions_TenantId_ConnectorId",
                schema: "dbo",
                table: "AnkTransmissions",
                columns: new[] { "TenantId", "ConnectorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkTransmissions_TenantId_IdempotencyKey",
                schema: "dbo",
                table: "AnkTransmissions",
                columns: new[] { "TenantId", "IdempotencyKey" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transmissions_ExactlyOneTarget",
                schema: "dbo",
                table: "AnkTransmissions",
                sql: "(CASE WHEN [PayoutId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PaymentIntentId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PartnerBillPaymentId] IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AnkRefunds_TenantId_PaymentId",
                schema: "dbo",
                table: "AnkRefunds",
                columns: new[] { "TenantId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkRefunds_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkRefunds",
                columns: new[] { "TenantId", "PaymentIntentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPayouts_DestinationExternalAccountId",
                schema: "dbo",
                table: "AnkPayouts",
                column: "DestinationExternalAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPayouts_TenantId_ClientReference",
                schema: "dbo",
                table: "AnkPayouts",
                columns: new[] { "TenantId", "ClientReference" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPayouts_TenantId_ConnectorId",
                schema: "dbo",
                table: "AnkPayouts",
                columns: new[] { "TenantId", "ConnectorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPayouts_TenantId_ProviderReference",
                schema: "dbo",
                table: "AnkPayouts",
                columns: new[] { "TenantId", "ProviderReference" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPayouts_TenantId_Status",
                schema: "dbo",
                table: "AnkPayouts",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_TenantId_ClientReference",
                schema: "dbo",
                table: "AnkPaymentIntents",
                columns: new[] { "TenantId", "ClientReference" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_TenantId_ConnectorId",
                schema: "dbo",
                table: "AnkPaymentIntents",
                columns: new[] { "TenantId", "ConnectorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPaymentIntents_TenantId_ProviderReference",
                schema: "dbo",
                table: "AnkPaymentIntents",
                columns: new[] { "TenantId", "ProviderReference" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderFulfilmentRefs_OrderId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderFulfilmentRefs_PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                column: "PartnerBillPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderFulfilmentRefs_PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                column: "PaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderFulfilmentRefs_PayoutId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkOrderFulfilmentRefs_TenantId_OrderId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderFulfilmentRefs_ExactlyOneTarget",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                sql: "(CASE WHEN [PayoutId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PaymentIntentId] IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN [PartnerBillPaymentId] IS NOT NULL THEN 1 ELSE 0 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AnkBillValidations_CatalogBillerServiceId",
                schema: "dbo",
                table: "AnkBillValidations",
                column: "CatalogBillerServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkBillValidations_ConnectorId",
                schema: "dbo",
                table: "AnkBillValidations",
                column: "ConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkBillValidations_TenantId_ClientReference",
                schema: "dbo",
                table: "AnkBillValidations",
                columns: new[] { "TenantId", "ClientReference" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkBillValidations_TenantId_ValidationToken",
                schema: "dbo",
                table: "AnkBillValidations",
                columns: new[] { "TenantId", "ValidationToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorBillerMappings_CatalogBillerId",
                schema: "dbo",
                table: "AnkConnectorBillerMappings",
                column: "CatalogBillerId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorBillerMappings_CatalogBillerServiceId",
                schema: "dbo",
                table: "AnkConnectorBillerMappings",
                column: "CatalogBillerServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorBillerMappings_ConnectorId",
                schema: "dbo",
                table: "AnkConnectorBillerMappings",
                column: "ConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorBillerMappings_TenantId_CatalogBillerId",
                schema: "dbo",
                table: "AnkConnectorBillerMappings",
                columns: new[] { "TenantId", "CatalogBillerId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorBillerMappings_TenantId_ConnectorId_CatalogBillerId_CatalogBillerServiceId",
                schema: "dbo",
                table: "AnkConnectorBillerMappings",
                columns: new[] { "TenantId", "ConnectorId", "CatalogBillerId", "CatalogBillerServiceId" },
                unique: true,
                filter: "[CatalogBillerServiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorCapabilities_ConnectorId",
                schema: "dbo",
                table: "AnkConnectorCapabilities",
                column: "ConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorCapabilities_TenantId_Category_CountryCode_Currency",
                schema: "dbo",
                table: "AnkConnectorCapabilities",
                columns: new[] { "TenantId", "Category", "CountryCode", "Currency" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorCapabilities_TenantId_ConnectorId_Category",
                schema: "dbo",
                table: "AnkConnectorCapabilities",
                columns: new[] { "TenantId", "ConnectorId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorInstitutionCodes_ConnectorId",
                schema: "dbo",
                table: "AnkConnectorInstitutionCodes",
                column: "ConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorInstitutionCodes_FinancialInstitutionId",
                schema: "dbo",
                table: "AnkConnectorInstitutionCodes",
                column: "FinancialInstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorInstitutionCodes_TenantId_ConnectorId_FinancialInstitutionId",
                schema: "dbo",
                table: "AnkConnectorInstitutionCodes",
                columns: new[] { "TenantId", "ConnectorId", "FinancialInstitutionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkConnectorInstitutionCodes_TenantId_ConnectorId_ProviderInstitutionCode",
                schema: "dbo",
                table: "AnkConnectorInstitutionCodes",
                columns: new[] { "TenantId", "ConnectorId", "ProviderInstitutionCode" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkExternalPayoutAccounts_ConnectorId",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts",
                column: "ConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkExternalPayoutAccounts_PartnerId",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkExternalPayoutAccounts_TenantId_BeneficiaryPartyId",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts",
                columns: new[] { "TenantId", "BeneficiaryPartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkExternalPayoutAccounts_TenantId_ConnectorId",
                schema: "dbo",
                table: "AnkExternalPayoutAccounts",
                columns: new[] { "TenantId", "ConnectorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialInstitutions_CountryCode_InstitutionType",
                schema: "dbo",
                table: "AnkFinancialInstitutions",
                columns: new[] { "CountryCode", "InstitutionType" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkFinancialInstitutions_TenantId_CountryCode_Name",
                schema: "dbo",
                table: "AnkFinancialInstitutions",
                columns: new[] { "TenantId", "CountryCode", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPartnerBillPayments_BillValidationId",
                schema: "dbo",
                table: "AnkPartnerBillPayments",
                column: "BillValidationId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPartnerBillPayments_ConnectorBillerMappingId",
                schema: "dbo",
                table: "AnkPartnerBillPayments",
                column: "ConnectorBillerMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPartnerBillPayments_ConnectorId",
                schema: "dbo",
                table: "AnkPartnerBillPayments",
                column: "ConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPartnerBillPayments_TenantId_ClientReference",
                schema: "dbo",
                table: "AnkPartnerBillPayments",
                columns: new[] { "TenantId", "ClientReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnkPartnerBillPayments_TenantId_OrderId",
                schema: "dbo",
                table: "AnkPartnerBillPayments",
                columns: new[] { "TenantId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPartnerBillPayments_TenantId_ProviderReference",
                schema: "dbo",
                table: "AnkPartnerBillPayments",
                columns: new[] { "TenantId", "ProviderReference" });

            migrationBuilder.CreateIndex(
                name: "IX_AnkPartnerWebhookEvents_ClientReference",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents",
                column: "ClientReference");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPartnerWebhookEvents_ProviderReference",
                schema: "dbo",
                table: "AnkPartnerWebhookEvents",
                column: "ProviderReference");

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

            migrationBuilder.CreateIndex(
                name: "IX_AnkPayoutReversals_PayoutId",
                schema: "dbo",
                table: "AnkPayoutReversals",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_AnkPayoutReversals_TenantId_PayoutId",
                schema: "dbo",
                table: "AnkPayoutReversals",
                columns: new[] { "TenantId", "PayoutId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AnkOrderFulfilmentRefs_AnkOrders_OrderId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                column: "OrderId",
                principalSchema: "dbo",
                principalTable: "AnkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnkOrderFulfilmentRefs_AnkPartnerBillPayments_PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                column: "PartnerBillPaymentId",
                principalSchema: "dbo",
                principalTable: "AnkPartnerBillPayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnkOrderFulfilmentRefs_AnkPaymentIntents_PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                column: "PaymentIntentId",
                principalSchema: "dbo",
                principalTable: "AnkPaymentIntents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnkOrderFulfilmentRefs_AnkPayouts_PayoutId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                column: "PayoutId",
                principalSchema: "dbo",
                principalTable: "AnkPayouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnkPayouts_AnkExternalPayoutAccounts_DestinationExternalAccountId",
                schema: "dbo",
                table: "AnkPayouts",
                column: "DestinationExternalAccountId",
                principalSchema: "dbo",
                principalTable: "AnkExternalPayoutAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnkTransmissions_AnkPartnerBillPayments_PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkTransmissions",
                column: "PartnerBillPaymentId",
                principalSchema: "dbo",
                principalTable: "AnkPartnerBillPayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnkTransmissions_AnkPaymentIntents_PaymentIntentId",
                schema: "dbo",
                table: "AnkTransmissions",
                column: "PaymentIntentId",
                principalSchema: "dbo",
                principalTable: "AnkPaymentIntents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnkTransmissions_AnkPayouts_PayoutId",
                schema: "dbo",
                table: "AnkTransmissions",
                column: "PayoutId",
                principalSchema: "dbo",
                principalTable: "AnkPayouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnkOrderFulfilmentRefs_AnkOrders_OrderId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropForeignKey(
                name: "FK_AnkOrderFulfilmentRefs_AnkPartnerBillPayments_PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropForeignKey(
                name: "FK_AnkOrderFulfilmentRefs_AnkPaymentIntents_PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropForeignKey(
                name: "FK_AnkOrderFulfilmentRefs_AnkPayouts_PayoutId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropForeignKey(
                name: "FK_AnkPayouts_AnkExternalPayoutAccounts_DestinationExternalAccountId",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropForeignKey(
                name: "FK_AnkTransmissions_AnkPartnerBillPayments_PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_AnkTransmissions_AnkPaymentIntents_PaymentIntentId",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_AnkTransmissions_AnkPayouts_PayoutId",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropTable(
                name: "AnkConnectorCapabilities",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkConnectorInstitutionCodes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkExternalPayoutAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPartnerBillPayments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPartnerWebhookEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkPayoutReversals",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkFinancialInstitutions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkBillValidations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AnkConnectorBillerMappings",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AnkTransmissions_PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropIndex(
                name: "IX_AnkTransmissions_PaymentIntentId",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropIndex(
                name: "IX_AnkTransmissions_PayoutId",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropIndex(
                name: "IX_AnkTransmissions_TenantId_ConnectorId",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropIndex(
                name: "IX_AnkTransmissions_TenantId_IdempotencyKey",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transmissions_ExactlyOneTarget",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropIndex(
                name: "IX_AnkRefunds_TenantId_PaymentId",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropIndex(
                name: "IX_AnkRefunds_TenantId_PaymentIntentId",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropIndex(
                name: "IX_AnkPayouts_DestinationExternalAccountId",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropIndex(
                name: "IX_AnkPayouts_TenantId_ClientReference",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropIndex(
                name: "IX_AnkPayouts_TenantId_ConnectorId",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropIndex(
                name: "IX_AnkPayouts_TenantId_ProviderReference",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropIndex(
                name: "IX_AnkPayouts_TenantId_Status",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_TenantId_ClientReference",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_TenantId_ConnectorId",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkPaymentIntents_TenantId_ProviderReference",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropIndex(
                name: "IX_AnkOrderFulfilmentRefs_OrderId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropIndex(
                name: "IX_AnkOrderFulfilmentRefs_PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropIndex(
                name: "IX_AnkOrderFulfilmentRefs_PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropIndex(
                name: "IX_AnkOrderFulfilmentRefs_PayoutId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropIndex(
                name: "IX_AnkOrderFulfilmentRefs_TenantId_OrderId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderFulfilmentRefs_ExactlyOneTarget",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropColumn(
                name: "PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropColumn(
                name: "PaymentIntentId",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropColumn(
                name: "ProviderReference",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropColumn(
                name: "RawResponseJson",
                schema: "dbo",
                table: "AnkTransmissions");

            migrationBuilder.DropColumn(
                name: "ClientReference",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "ConnectorId",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "PaymentIntentId",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "ProviderReference",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "RawResponseJson",
                schema: "dbo",
                table: "AnkRefunds");

            migrationBuilder.DropColumn(
                name: "ClientReference",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "ConnectorId",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "ConvertedAmount",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "DebitCurrency",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "DestinationType",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "Fee",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "FeeCurrency",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "FxRate",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "Narration",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "OrderItemId",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "ProviderReference",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "RawResponseJson",
                schema: "dbo",
                table: "AnkPayouts");

            migrationBuilder.DropColumn(
                name: "ClientReference",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "CollectionMethod",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "CollectionStatus",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "ConnectorId",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "Fee",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "FxRate",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "MaskedPhoneNumber",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "MobileNetwork",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "NextActionMode",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "NextActionRedirectUrl",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "NextActionUssdCode",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "ProviderReference",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "SettledAmount",
                schema: "dbo",
                table: "AnkPaymentIntents");

            migrationBuilder.DropColumn(
                name: "PartnerBillPaymentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropColumn(
                name: "PaymentIntentId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs");

            migrationBuilder.DropColumn(
                name: "AmountType",
                schema: "dbo",
                table: "AnkCatalogBillerServices");

            migrationBuilder.DropColumn(
                name: "FixedAmount",
                schema: "dbo",
                table: "AnkCatalogBillerServices");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<Guid>(
                name: "PayoutId",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastError",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                schema: "dbo",
                table: "AnkTransmissions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PaymentId",
                schema: "dbo",
                table: "AnkRefunds",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkRefunds",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "dbo",
                table: "AnkRefunds",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "AnkPayouts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "dbo",
                table: "AnkPayouts",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<Guid>(
                name: "PayoutId",
                schema: "dbo",
                table: "AnkOrderFulfilmentRefs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
