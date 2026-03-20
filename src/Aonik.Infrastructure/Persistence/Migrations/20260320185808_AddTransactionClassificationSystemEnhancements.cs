using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Aonik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionClassificationSystemEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("1bee5ee1-7f45-0bd0-5637-2f0a8016bc33"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("27dabf45-052c-8784-2620-6c70523d0f64"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("2ab4a16d-91d7-c1d0-25ff-69715bdecded"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("49de988e-ecea-4a92-bffc-95622e79be91"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("70fd4eec-36ef-e460-ccf0-6361d3bb517b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("b7981080-c87f-d631-1313-6a16c3b4f063"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("cda09b4b-2680-24f9-273e-2379bb687a3b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("e80f8dd4-d1a0-8d96-24f5-4954356423ac"));

            migrationBuilder.AddColumn<string>(
                name: "SubCategory",
                schema: "dbo",
                table: "AnkCategorisationRules",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                columns: new[] { "Id", "AppliesToAccountId", "ApprovalStatus", "CaseSensitive", "Category", "CreatedAt", "CreatedBy", "CreatedFromUserCorrection", "DeletedAt", "DeletedBy", "IsActive", "IsDeleted", "MatchType", "MaxAmount", "MinAmount", "Pattern", "Priority", "Scope", "SubCategory", "TenantId", "UpdatedAt", "UpdatedBy", "UserId" },
                values: new object[,]
                {
                    { new Guid("007be53c-0dfa-2096-3545-074629fbb535"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Air Peace", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("02edee53-3dfa-844d-b0cb-8b971d51da30"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Cleanshelf", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("060c5ea6-db5a-cac8-23b4-fdc4933f2ff5"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Transfer Charge", 100, "System", "card_fee", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("0615b55c-5d6d-f812-c565-e12546db0d70"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Ebeano", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("07b81b63-5ff1-e084-74c8-66eab183c12c"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Transport for London", 100, "System", "public_transit", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("08583478-5048-686c-5b79-48b13c3dd656"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "PHED", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("08a9c57a-90ac-15ab-c1b9-aacb0d2a26fd"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Chicken Inn", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("0ab8417a-6cf9-d9cd-287e-5ee98fd3c8ea"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Amazon Prime", 100, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("0ad86bfd-959a-01f8-f838-70f46cbe4211"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "University of Nairobi", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("0b4e37a3-1634-20d2-0777-b7a32319199d"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Thames Water", 100, "System", "water", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("0d2ed130-1736-b2d6-a30e-615198c71fb9"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "LloydsPharmacy", 100, "System", "pharmacy", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("0d578577-56d6-941d-c3a9-de333b3caa7d"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Halfords", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("0ea50297-d89c-8357-718c-807a9713f9db"), null, "Approved", false, "savings", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "PiggyVest", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("109124b2-4db4-9542-aa24-ce80e9fe2d9c"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Wise Transfer", 90, "System", "remittance", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10ab5979-cfc4-0c73-a2c9-3d9004a1272f"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Now TV", 100, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10f6dcbe-8c3a-c31d-e173-92a3af60ec85"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Superdrug", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1144e611-d4d0-cee0-70ef-68a0ed884526"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Dominos Nigeria", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("11c62645-6844-a660-d2ef-66d9d9de62b6"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "MPESA", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1203624d-aeb8-0e56-507a-92f4b0e380ca"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "SLC", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1262b5c8-2ca2-e0e3-4a26-81efbb785561"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Kenyatta University", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1284d9d7-8a6f-7802-e325-6c28f832a7bb"), null, "Approved", false, "travel", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Skyscanner", 100, "System", "booking", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("13123ec1-d1b3-f786-ad82-29bd1d6740e3"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Remitly", 100, "System", "remittance", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("13673659-286f-72b5-97c8-8d58707182df"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "IBEDC", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("142227a9-1e2e-a2f7-1f1b-98b0c4fb0e4d"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Prince Ebeano", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("14504f0a-111d-541c-d4bb-11585c820ed1"), null, "Approved", false, "entertainment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Steam", 100, "System", "gaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("14b648fd-cc76-9509-cbab-6241c4cc7363"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Aga Khan Hospital", 100, "System", "hospital", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("14f049fe-165a-ab1d-4ce5-87eee3775998"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "NHIA", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("15b44103-b0d0-e2bd-c5a8-a9dbcad5114a"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Skillshare", 100, "System", "courses", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1792c1ac-6dab-2880-b77c-0effe7834962"), null, "Approved", false, "travel", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "EasyJet", 100, "System", "flights", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("17e25fb5-cd3d-bbf2-d68c-174e8a3352d2"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "TotalEnergies Nigeria", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1926bac4-b888-31b9-9e72-676c1d8a95b4"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "BT", 100, "System", "internet", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("19d1b394-f98e-1ede-7fda-3ae77d7adbfe"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Glovo", 100, "System", "delivery", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1a1b013a-6880-e269-ec16-477eb8d20c43"), null, "Approved", false, "fitness", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "PureGym", 100, "System", "gym", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1b372ef4-9eba-01bd-e7a9-fccff0519a76"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Co-operative", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1b96586f-f941-ba60-6dec-e71fb7abcb00"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Paramount+", 90, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1c50fbef-5680-a983-4c67-7f765c66a011"), null, "Approved", false, "housing", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Foxtons", 100, "System", "rent", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1c766908-b0fc-1b0d-d15e-c7e72a97f1c1"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "iCloud", 100, "System", "cloud_storage", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1cabdf53-9a38-23b0-d1ba-9a851ccf6859"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Texaco", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1d6c441b-aa05-637b-e540-e8be92e5aa0a"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Three", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1ee634c9-3688-afdf-04c2-0bb37266fea1"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "SMS Alert Fee", 100, "System", "sms_alert", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("1f085df3-a282-c925-a17b-c6cb0749d07d"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Apple.com/bill", 100, "System", "software", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("20c97a56-7a3b-1679-9701-7c0a09d41e07"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "9mobile", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("214636a3-5ce4-11a4-96d6-bbe742495693"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "BEDC", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("214a14d9-70c5-a513-6c2f-15ed71c506bb"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Stamp Duty", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("21a232bf-b185-6618-5761-0cc303376da8"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Franko Trading", 100, "System", "electronics", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2380d1e7-e476-7b00-64f4-0bf11bf41cd7"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Korle Bu", 100, "System", "hospital", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("238f99d1-c16b-1de8-9751-9894d316f417"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Coursera", 100, "System", "courses", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("243e7cce-70bf-0112-5d8c-0b78d8ac887b"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "KFC Ghana", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2498fcdc-63ef-3b0d-85e6-51646d31ca74"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Quidax", 100, "System", "crypto", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("24c8e561-4bfb-f8f5-cf19-b7b88a461a38"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Accra Mall", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("25a44c45-3504-86b9-958c-baaf7ab1d482"), null, "Approved", false, "entertainment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Odeon", 100, "System", "cinema", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("25bab3e9-baab-2050-b7fc-aa25de93d8a8"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "KFC", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("25e61c86-fa2f-8b94-f8ab-452212692a52"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Chicken Republic", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("27b2f2f6-020f-1d7b-c806-d8b40fbbe819"), null, "Approved", false, "travel", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "British Airways", 100, "System", "flights", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("27c8442a-6588-32fc-c074-ca806b2c2d97"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Disney+", 100, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("27ee3f9e-206b-b7f7-d5c7-de6199445cdb"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Bupa", 100, "System", "doctor", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("288408b4-3431-5931-1eff-158be195f967"), null, "Approved", false, "entertainment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "PlayStation", 100, "System", "gaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2a3033fb-b74d-b824-5f2d-2735b3d700d0"), null, "Approved", false, "fitness", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Virgin Active", 100, "System", "gym", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2a585bfd-9cd5-ae05-c54c-f96cd7b4cdd8"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Primark", 100, "System", "clothing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2b0d9fdd-64ec-4c94-1b0a-b903ce81250d"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Overdraft Fee", 100, "System", "overdraft", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2b233658-6811-83f7-9565-562de3a013eb"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Java House", 100, "System", "cafe", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2baf7028-9d03-3099-5e74-cd4766f5994e"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Hargreaves Lansdown", 100, "System", "stocks", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2c3d20f8-f6ea-88b1-5d9e-c96648615a3e"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Netflix", 100, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2c5ba3bf-4b69-85cb-8624-838d08f758d9"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Chowdeck", 100, "System", "delivery", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2cde20ab-f874-992f-ba53-d71838913b18"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Zakat", 90, "System", "religious", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2d002bfe-e866-31e9-bb36-3f599fbcd885"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "NHS", 100, "System", "doctor", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2dae7534-3d9a-3f45-f639-cae27c3e06af"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Nairobi Hospital", 100, "System", "hospital", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2ddb71af-1b00-5db9-6737-ae5132808bb5"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Palace", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2e1ffa06-0a13-5fa5-24bc-7d90926cf299"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Crunchyroll", 100, "System", "music", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3084f92b-6cdc-cd75-767d-316f5c781d6c"), null, "Approved", false, "loan_payments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "PayPal Credit", 100, "System", "credit_card", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("315d8328-8abd-5dd9-1bed-4a0bc644af6c"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Bolt Kenya", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("31613adf-b097-235a-3913-bbcdaa574ef3"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Tantalizers", 100, "System", "restaurant", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("31c31e3d-506a-980f-5978-09a1e171f67f"), null, "Approved", false, "fitness", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "JD Gyms", 100, "System", "gym", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("329d121d-f713-736c-ff87-c11ac7da6884"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Kenya Power", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("33770c5a-6e01-9f09-ef63-3bbd619ea2c6"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Currys", 100, "System", "electronics", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("339cdda1-a54b-3971-7abb-81b736ae7a2a"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "OpenAI", 100, "System", "software", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("347fe8cb-ad36-88eb-527d-452854dc716f"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Bulb", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3520b2fb-f1e4-0911-226f-c76eecf69741"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "O2", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("365bcb04-61d1-3b18-4f4a-b7d940ca0ffa"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Deliveroo", 100, "System", "delivery", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("36b11130-7702-b31a-af71-3e85de2b6602"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Nairobi Water", 100, "System", "water", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("37649cc3-c5c3-fabb-f37f-8beb33fcbdf0"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Costa Coffee", 100, "System", "cafe", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("389434df-8de4-756d-4eb1-27491abe4928"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Mr Biggs", 100, "System", "restaurant", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("39be3c69-c461-8ce1-5a7d-9f359c18c320"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Uber", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3a233b55-6559-b079-a5a8-eaff86e88fad"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "NHIF", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3a571dae-0fc6-d314-9125-b47a7f40bc60"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Charity", 90, "System", "donation", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3a57c8c4-33d6-d80f-7d97-cb24ccbe94d5"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Masoko", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3c93c35a-1e11-caa0-0d05-a2f7e54ae6b5"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Council Tax", 100, "System", "council_tax", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3d4388b5-70e7-d982-4f4a-db8d60480236"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Bolt Nigeria", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3d4ba0fa-c87d-e95f-8ffb-aa79485b2e59"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Boots", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3d6bf7ab-bcc2-6a49-82a9-b7be86e62bfe"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "MTN MoMo", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3d7ee515-59ae-4213-5ce4-96be5b61f1f4"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Burger King Ghana", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3d836b9d-9926-ef2b-0065-a7764e5ad1d7"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "NEPA", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3dba8bdf-9c90-1e19-d600-3aa61af31016"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "AEDC", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3e299cc5-acaf-8d20-f38e-ca01caf4479c"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Chipper Cash", 100, "System", "remittance", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3f181020-30da-6641-9ff4-308fc1bac9e2"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Greggs", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3f1e53c1-6ccd-9d90-ca76-b4e98ac8a665"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "ASOS", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("40b6d7cb-202a-2b9a-a991-457dd3fc646e"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "AJ Bell", 100, "System", "stocks", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("40b8a372-04e8-cf69-7e61-1709bb8cfd6b"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Tesco", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4174154c-e645-5d18-9266-ab5353d44dc1"), null, "Approved", false, "fitness", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Nuffield Health", 100, "System", "gym", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("41894531-3785-6524-bdd9-0f2fe349e003"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Lagos Water", 100, "System", "water", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("43e00cf2-f815-bfd1-cb69-54a8dd35bf21"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "TransferWise", 90, "System", "remittance", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("44572c8d-e874-b4b4-3157-193793284468"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "KNUST", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("470d03b5-14d7-927b-57e0-7222882e749e"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Artcaffe", 100, "System", "cafe", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4793ddad-d69a-2092-d667-cf48d24593df"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "DSTV Ghana", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("47b2ba5f-97ba-4324-1ad8-64c42b421db5"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "NECO", 100, "System", "exams", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("49091964-016e-61b4-65bc-7b9ff452883e"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "ECG", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4926443d-b391-b9d8-fae1-546a764e482c"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Mobil", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4978bfc9-8e4d-1e26-edb1-44c9300d6aef"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "UNICEF", 100, "System", "crowdfunding", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("49a7bdaa-6b3f-6acc-7d06-3152c3a0f431"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Bolt", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4a0811d6-320b-02ec-138d-fd2855b584aa"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "eBay", 90, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4a2c331c-e8f1-10d5-ea04-f3ce62a01114"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "LASU", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4cdb4d57-fd88-93b8-f40a-3475f69c36c4"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Binance", 100, "System", "crypto", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4d4fa736-6bbc-e3b3-726f-238a2410f4d9"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Reddington Hospital", 100, "System", "hospital", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4d97510e-bf64-be4c-2a76-9bd3fe44ee3c"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "EKEDC", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4dc0dd32-28b2-7a7b-c407-300c9581fb8b"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "SPAR Nigeria", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4dd1d566-48d1-10bc-6fd7-4426e76283fd"), null, "Approved", false, "gifts", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Hallmark", 90, "System", "present", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4f28b2ff-0551-c8d9-db9a-7f539b0302e8"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "EHA Clinics", 100, "System", "hospital", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("4fefd6e4-085f-8521-2855-c9b0a2c21d4b"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "YouTube Premium", 100, "System", "music", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("506e3d6f-67b4-fea1-7ad3-82960744f2db"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "M-Pesa Send", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("52f0ff1b-dc94-2c8a-9b88-a23191763864"), null, "Approved", false, "housing", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Rightmove", 100, "System", "rent", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("558c09c4-f4fb-c077-4405-e02f08dc697d"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Nando", 100, "System", "restaurant", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("55f0a918-6e30-b290-04bc-09b7fe68a39b"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "British Red Cross", 100, "System", "donation", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("575fd074-581c-684e-3838-31f042c8edde"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Computer Village", 100, "System", "electronics", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5779fea0-2797-1a36-67e8-7af773772aab"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Jambojet", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5955c656-1833-9322-b1e0-c64f2081bc10"), null, "Approved", false, "loan_payments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Clearpay", 100, "System", "bnpl", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5a4ecb71-120c-154a-c7e8-ced630690af3"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Jiji", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5aa34361-e2aa-36af-9970-f71a3188ee70"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Uber Eats Kenya", 100, "System", "delivery", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5af92690-1a25-bcda-42be-d5e08c733604"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "HealthPlus Pharmacy", 100, "System", "pharmacy", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5b60bc26-0fc6-ba2a-77da-fd7f1c75218f"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "NNPC", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5dad92d9-82ab-0422-d1af-3a35df9bd8d8"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "InDrive", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5df9257e-8a52-c5ab-b0cd-a9ae30f28f3c"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Nutmeg", 100, "System", "stocks", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5e2ef72f-7dbd-0865-8798-3894ab649b2b"), null, "Approved", false, "entertainment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Eventbrite", 100, "System", "events", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5e78aee0-6158-2a90-bb96-5d43bbb5f47a"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Startimes", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("5ea4a475-df51-0eb4-0351-b25ca2e7c7c5"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Wagamama", 100, "System", "restaurant", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("6042c612-fd17-77ba-30d0-3d8d5795d84f"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "DSTV", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("61bcc1ce-7d5c-8783-b7c2-3e350f25c3ef"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "McDonald", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("62bb8eae-ed68-b250-c7c2-2b6ccebd3c0f"), null, "Approved", false, "travel", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Booking.com", 100, "System", "booking", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("62d4aa77-e7fd-3261-fa87-2f3fbe94a7d0"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "VAT", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("62e05bc9-01e2-19f4-3f23-52887630a2e7"), null, "Approved", false, "travel", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Ryanair", 100, "System", "flights", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("63341952-970f-ed05-db7e-5c6299fb434f"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Marwako", 100, "System", "restaurant", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("6529f2e0-0e40-f882-33d9-9e1fc20fe503"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Octopus Energy", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("67739ecc-a8f8-6e39-b405-c0c47aaf5fe5"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Flutterwave", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("677af6da-dde8-191c-3231-8e5d30298957"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "EDF Energy", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("68e00c9b-b16f-c643-8e42-99d19d4d5db5"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Hubmart", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("6924ad96-5eac-9345-ec08-6f34f12b8c03"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "NIBSS Fee", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("69abfe80-7e2f-aa9b-e49e-91c3e711e73d"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Next", 100, "System", "clothing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("69d62e03-c729-bece-40bb-fac41a1fd9d8"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Uber Kenya", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("6a41182e-dbf0-6a33-22b8-394697d222ea"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Glovo Nigeria", 100, "System", "delivery", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("6bc85ecd-45f5-ec1a-6cf6-5c4f52bf2799"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Kilimall", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("6c6b281b-05f8-19df-0b62-cc82ea721f6b"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Just Eat", 100, "System", "delivery", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("6cc322f1-9f65-09b0-a444-01987d79f4a4"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Vodafone UK", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("6daf1878-de96-0422-3343-5b533caa00cd"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "IrokoTV", 100, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("6e2b7937-756e-64b7-82bc-c90ce49a2953"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Pizza Hut", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("6fd7554e-27ee-f44e-0ccb-55cff9bf9e3a"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Giffgaff", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("703383dd-afe5-d4ba-1de3-ff5dca5936cd"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Lister Hospital", 100, "System", "hospital", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("709c0b9b-03a9-a187-3361-21596979ec9e"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Sky", 100, "System", "internet", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("730f8209-5719-76f4-37ca-48fdb38f40c6"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Argos", 100, "System", "department_store", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("73e00c89-4b02-5cdb-484f-becaba8d161e"), null, "Approved", false, "pets", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Pets at Home", 100, "System", "supplies", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("752b75e5-3013-f935-57ba-65f98aefc7a6"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Game", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("79c45e5a-2c01-97df-ab83-69b760fe4f70"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Oando", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("7daf1e52-b01d-05fc-4334-4e782ada9021"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "British Gas", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("7e38a699-ccb9-f753-6151-0223d57a2d4e"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Specsavers", 100, "System", "optical", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8030157b-2a4d-f73d-eb72-3a8ca318e805"), null, "Approved", false, "pets", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Vets4Pets", 100, "System", "vet", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("817f25be-3625-0907-e92f-f5b0777f592c"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Audible", 100, "System", "music", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("81fc0724-54bb-8b7e-3268-ab2e10a84d8b"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Student Loans Company", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("82e73bcd-94bc-c4ac-2e33-f716b242cdfe"), null, "Approved", false, "fitness", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "The Gym Group", 100, "System", "gym", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("83340abd-9804-f4b6-f8d9-ffc037b90e3f"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Lloyds Pharmacy", 100, "System", "pharmacy", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("83e0a9fc-8424-c69b-e1ac-5d1d0098111a"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "ATM Fee", 100, "System", "atm", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8468c179-47e1-124e-b9b4-37e3840d1fc4"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Card Maintenance Fee", 100, "System", "card_fee", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8529ad67-078b-7625-40cf-02bf3b3e9ab5"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Disney Plus", 100, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("86b44be6-ad6b-51a3-0a1b-f39aab98c6a9"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "JAMB", 100, "System", "exams", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("86d8cd0f-4d6e-5b41-5939-24cd1e590fdc"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Five Guys", 100, "System", "restaurant", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("878ddd55-1b01-6cfa-e647-3d736d41b941"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Kuda Transfer", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("87a5a291-f6df-bf2f-a9d6-34acc8ee4303"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Morrisons", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("88598591-7572-50a5-2b0d-305ef14228e5"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Strathmore", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8864a762-0e39-3eba-7994-5a53533970d8"), null, "Approved", false, "loan_payments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Afterpay", 100, "System", "bnpl", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8923f640-34cd-c0e9-6585-4e6195e0c3f0"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Monthly Account Fee", 100, "System", "card_fee", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8942d49d-440f-476f-9e0d-5ad13c92d31a"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Jiji Ghana", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8a8db70c-0705-9bf4-afe6-c31c79ae44bf"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Chandarana", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8ae66331-e65d-a917-475b-e7600ab59463"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "GOtv Ghana", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8b548b64-5645-1a7f-5ba2-96953a1c569e"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Koala", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8b76873b-ea55-74e4-c32d-bffc82f372cf"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "The Place", 100, "System", "restaurant", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8bb33c49-edf3-bf54-9daf-7084e335fb56"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Glo", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8ce14935-0bdd-3089-2539-48e80be6cdb4"), null, "Approved", false, "travel", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Hotels.com", 100, "System", "booking", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8db85ddf-116b-5fcf-26e1-851b6c1b9cc1"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Shell Ghana", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8f8c6a2c-43d8-55af-19b2-1bf283e3b615"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "JustGiving", 100, "System", "donation", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8fc144af-86ad-64eb-cc07-e28b85c7f6ff"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "TK Maxx", 100, "System", "department_store", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("91e7175c-ae95-5e58-d701-dc178b55eaae"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Airtel Kenya", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("92556850-85c0-3ca3-c05a-9a542a3449c9"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "OPay Transfer", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("92b231fd-b634-c750-983a-af8486d35cd8"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Virgin Media", 100, "System", "internet", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9314f400-5f9a-3814-f147-527e85775ab0"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "University of Ghana", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("95c47ff7-cf1e-940e-3a26-02e70e689349"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Equity Transfer", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("96db17fe-2720-933f-aef0-862cc4f57789"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Luno", 100, "System", "crypto", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("970f629f-051c-4599-f713-2e77c2a5741f"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Jumia Ghana", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("98486149-3de5-c9cc-b8c4-6b5104cf135c"), null, "Approved", false, "savings", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Bamboo", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("98597aae-5a4f-80f2-6b83-eea2a5a5c91b"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Card Replacement Fee", 100, "System", "card_fee", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("98860d54-2db5-cec4-eec2-fd4b962518d2"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Jumia Kenya", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("99948e67-7b93-b4b5-0ee1-2b311f08747b"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Western Union", 100, "System", "remittance", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9a093889-105b-6128-20de-0c08f2c249a9"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "UNILAG", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9a11044a-2b06-ec27-df4e-6a8fbc2f5a22"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "TV Licence", 100, "System", "tv_licence", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9a50b8a4-f4b7-79bc-a3ee-35e980ae718e"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Lidl", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9ab13ed7-39c1-835a-3349-2e98ed79e42c"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Co-op", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9aeb73aa-60b0-f622-127a-0715ac328d62"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "United Utilities", 100, "System", "water", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9b7a5557-d535-7659-7a65-39ce19d2180d"), null, "Approved", false, "housing", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "OpenRent", 100, "System", "rent", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9b7ac41b-475c-e28a-b4ae-2a5e57cfa6a9"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Zuku", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9b92eb36-b723-d281-643b-c1b74cc091fd"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "GOtv", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9c5c5ca0-58c5-9c0b-2ed4-b816cfaa480a"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Shoprite Nigeria", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9c9c4854-d3fa-8a80-2495-c08ef79c9026"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "SSE", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9cc1c74b-7ece-b6bf-8954-88384ea7f0d0"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Iceland", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9ebc5207-8c4d-858e-01f2-bc5a1ac231d1"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Starbucks", 100, "System", "cafe", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9ec7a5ea-d311-026f-e313-1b80469c5487"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Spotify", 100, "System", "music", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9f4bf3d4-db27-73b4-4372-49abee1145ec"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "JKUAT", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a0157292-287b-5b6e-59ca-de475839b819"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Addison Lee", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a072e78a-c458-911f-f707-feba5bd904f4"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Jumia", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a14aa52f-e717-604f-d45c-0c6cdaabac2e"), null, "Approved", false, "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Medplus Pharmacy", 100, "System", "pharmacy", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a15919be-1396-9585-989a-2dec432c455c"), null, "Approved", false, "entertainment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Xbox", 100, "System", "gaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a1808d71-b29e-8b2d-0d71-143325b85f64"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "PayPorte", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a185907c-ab58-5956-877a-30de20282c9a"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Safaricom", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a535dd7e-0f55-87ab-cea7-d80602a15d1d"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Tithe", 90, "System", "religious", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a6f7497c-1c04-615a-280c-cdfbadacfa43"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "GOtv Kenya", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a7d79f9b-8654-95a8-b5a2-8e69d4f539bf"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Shell", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a8587738-3a7f-2dcf-02da-3f1faae19980"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Carrefour Kenya", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a8bcf3ef-2669-aad7-a638-f8c0435668af"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "EE", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a8cf30c2-e287-2945-dc6f-7456a292770c"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Melcom", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a927a565-a190-6892-0330-5144e1458a05"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Uber Eats", 100, "System", "delivery", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a95d7231-365c-6ecf-7ade-2e4f39591b90"), null, "Approved", false, "entertainment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Ticketmaster", 100, "System", "events", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("aa414c39-8705-1375-0f6d-cedccc8acd01"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Google Storage", 100, "System", "cloud_storage", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("aa6613ba-cefd-f34a-ea65-c24565a068ee"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Pret A Manger", 100, "System", "cafe", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ab03dbb3-493a-bbb4-fbf7-f5f7326b1016"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "BP", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("aca69a6c-aac9-c5a7-01d1-cf209840cd4a"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "AirtelTigo", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("adb51488-ef35-b8d4-c274-9cca7f7b2ef4"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "GOOGLE *", 90, "System", "software", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b03e1ad3-9bb2-76b7-17d9-4b21269f40e3"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Shoprite", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b1a4de04-ef5e-1cf2-fe7d-82f175e235b0"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Apple.com", 90, "System", "software", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b1b2dd5b-174c-81b8-b50e-2fb0554ec429"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Farmfoods", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b377af45-63b0-1997-91c5-ba93f90b8ef2"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "H&M", 100, "System", "clothing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b4593d3e-3dc4-8e4c-492a-0da325c7e9ad"), null, "Approved", false, "entertainment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Nintendo", 100, "System", "gaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b5b4f64d-0780-2224-2a91-6223028a53d9"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Scottish Power", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b63e2211-fca2-1068-30da-6e1151036f31"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Caffe Nero", 100, "System", "cafe", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b7b6c768-76ca-f334-44d4-eef1f1c69f71"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Microsoft 365", 100, "System", "software", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b86ad24f-998d-8152-764c-734d7adecc4b"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Little Cab", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b8dd92b4-bb3a-9004-cc50-1d51a4d3ce97"), null, "Approved", false, "entertainment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Cineworld", 100, "System", "cinema", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b94f8ee1-fffb-18a3-7790-c0482f321eea"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Church Offering", 90, "System", "religious", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("b9d09e71-0d2b-a433-65a5-bc5bfd2a7eb9"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Azimo", 90, "System", "remittance", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ba835fc2-eebb-2ebb-50fc-9be93e30109e"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "GoFundMe", 100, "System", "donation", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("bb42de6d-7124-8485-49c8-782ef0fca3ad"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "TotalEnergies Ghana", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("bbd28d80-dba9-d0ef-2e9d-9857d0c839e5"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Ghana Water", 100, "System", "water", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("bcc1b680-d694-558d-d44e-36cec4588941"), null, "Approved", false, "fitness", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "David Lloyd", 100, "System", "gym", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("bd3ed9c2-b56c-73f3-04a3-3430825fb8e5"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "PalmPay Transfer", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("bdf26eab-d17c-b089-0df1-450076468323"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Marina Mall", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("be6f1d3c-3a3f-55c8-d458-05dfe1d3919f"), null, "Approved", false, "personal_care", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Treatwell", 100, "System", "beauty", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("bf8cdcc5-541a-29ae-5305-ec0e29e98878"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Dominos", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c02d6f93-6fe1-d602-ede3-4ca9f203c568"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Alibaba", 90, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c090db3d-91f1-baeb-6b6c-e4779a7558a3"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Ocado", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c0b89dbe-3c14-7851-f80f-4923d269efe3"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Twitch", 90, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c170d2e6-5365-bdd1-d82a-1756765ea71e"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Justrite", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c17dd3d1-c9ff-1bea-35b9-fca42665b98a"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "KFC Kenya", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c1e68af4-aecd-b08c-1f2a-a4a726934d46"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Kilimanjaro", 100, "System", "restaurant", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c214e6c6-b18d-7836-7c68-7cb96362b31f"), null, "Approved", false, "savings", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Risevest", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c4e68fd0-a5ea-1d6f-0b71-058afbc9ade6"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Airtel Nigeria", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c53e9f62-7e79-d045-812b-a5703dac3c08"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Konga", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c6b4f046-4cd6-67da-eafd-76e2afcda05b"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Pizza Inn Kenya", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c6b87f54-ad07-a8b4-bc1a-0f71d51e886e"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "HBO Max", 90, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c71d5d1d-b93a-6a01-ec3a-13230697828d"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "WAEC", 100, "System", "exams", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c78f322c-868e-7ca8-d185-d9d08db967d5"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Jiji Kenya", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c7bb37ad-b6b5-6b25-6480-8fd83501817f"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Naivas", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c7f31def-6c64-b317-5684-6e8d9167387c"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Trainline", 100, "System", "public_transit", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c8a97f74-5706-fb6d-1bd5-9f05247f22e7"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Uber Nigeria", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c93ebefe-ae03-8418-d5d1-c80c474db05f"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Burger King", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c9c1a562-b685-e750-0ace-1e86a7dc0891"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "KPLC", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c9d38686-1be0-05c8-8a1f-1aec05484f94"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "AliExpress", 90, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("cade5b35-62aa-00da-d96f-5aaf46b69f60"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Goil", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("cae67098-cfb5-d528-1ba4-c72bab5bd014"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Hulu", 90, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("cae80a91-77a6-b9ed-f2e4-0cbe2d4e3970"), null, "Approved", false, "travel", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Airbnb", 100, "System", "hotel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("cb3cd92e-a1d0-00a9-971c-0f7e0b3d349f"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Vodafone Ghana", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("cd15110c-6519-671f-3cf3-bbad0db6ad06"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Wish.com", 90, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("cd94ea76-1217-d7ce-f31c-e10378c418ae"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Tonaton", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("cd9fb1db-b47c-79a5-9875-0eeebbe7bd89"), null, "Approved", false, "travel", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Expedia", 100, "System", "booking", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ce041c66-d307-49b5-0974-9f2b0780685e"), null, "Approved", false, "entertainment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Vue Cinema", 100, "System", "cinema", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("cf40c4bd-5883-7f68-0180-fd782e84318f"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "ChatGPT", 100, "System", "software", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d229e8ee-2457-d215-d875-9da243d1dcd5"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "John Lewis", 100, "System", "department_store", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d2854f4f-8da9-980a-5f1e-c854ae8d37b8"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Covenant University", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d2b55aef-d43f-138a-b462-600516b8ca99"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Next Cash and Carry", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d2b8887b-68c5-b038-1624-123dce7bdd2e"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Quickmart", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d4116d58-6628-398c-99c6-9736cacc0f70"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "TotalEnergies Kenya", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d4de5b0a-c321-bfc9-5c1e-d3bfebce7acf"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Arik Air", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d5a0a961-48b6-12ab-bd72-f47b92b7f605"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "E.ON", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d6ac4184-f710-161c-0fa9-ce3f4187c086"), null, "Approved", false, "loan_payments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Klarna", 100, "System", "bnpl", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d718932e-3ea9-cc45-99cc-fc8e79fe085f"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Showmax", 100, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d82c0c18-649f-86b5-3154-a2e3a5355e41"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Shell Kenya", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d90256d9-f693-4bf7-f3e3-2014541d292f"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Coinbase", 100, "System", "crypto", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d9043e88-0860-90ca-6ddf-0f4b2c167675"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Amazon", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("dccf84e3-fdb4-10d3-39ee-ce67be264d3a"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Glovo Kenya", 100, "System", "delivery", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("dd2136cc-9f43-e5ba-0283-3c199a25ecb3"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "M&S Food", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("dd6cec94-333c-cc0b-d040-4def7a6d03f2"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Trading 212", 100, "System", "stocks", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("dd73c564-6956-ba04-d049-703c2662d47c"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "AMZN", 90, "System", "streaming", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("df139fa9-0cee-b864-6ee8-f180bddccf63"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Uber Ghana", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("dfec493f-a528-9277-096c-1facffc5653c"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Deezer", 90, "System", "music", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("dff98fc8-0305-6a82-15b5-e3e4177ad72c"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "IKEDC", 100, "System", "electricity", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("e0199745-19d9-5e4c-e536-4f98ccd54fe6"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "McDonalds", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("e01ee21f-2bad-116f-6eef-8cfd652070a1"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Sendwave", 100, "System", "remittance", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("e2435fbb-838a-2756-f0d6-56ea43172b83"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Cancer Research", 100, "System", "donation", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("e3546581-25fc-0c7c-f322-822b622d7ab9"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Udemy", 100, "System", "courses", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("e4b61e17-2616-61d7-bbbb-68acc79ddd44"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Telecel Ghana", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("e658c66f-8647-0377-6d02-8774af2d3e77"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Max Mart", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("e6c45a12-4925-e66f-6534-c31f9c24bb82"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Asda", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("e813d5a7-66f6-d007-bc2f-a13839693dc8"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Esso", 100, "System", "fuel", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("eb102201-4f40-239a-c95c-1ddae578f0a6"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Freetrade", 100, "System", "stocks", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("eb89efc0-d77f-1156-8fa7-4d089b93caf9"), null, "Approved", false, "personal_care", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Headmasters", 100, "System", "haircut", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ebad1dd6-e76e-b07c-a880-a6a1967333f9"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "OAU", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ec797db5-9afd-f1b9-8d32-2468769d34dd"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "UCC", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ece344a6-b5d8-8708-d9ce-095f4772784f"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "M-Pesa", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ed5c70c7-e763-283a-fda8-47f80a8309d2"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Temu", 90, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ed9aee1e-6262-3094-53e5-2566d073d7c5"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Roqqu", 100, "System", "crypto", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("eec8856d-35ef-2a82-4434-f15902df5f67"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "MTN Ghana", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("eed6c1d3-2d3a-04da-2952-d8f2c4123469"), null, "Approved", false, "bank_fees", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Foreign Transaction Fee", 100, "System", "foreign_tx", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("eee5f1b2-1c79-3bb2-4767-caf08c0a9697"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "TFL", 100, "System", "public_transit", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("efafa623-6c2f-c2e1-304d-e1a4ffdac625"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Donation", 90, "System", "donation", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("effa9f3f-833e-a3fc-6b4c-d9a94742c922"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "MoneyGram", 100, "System", "remittance", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f0388474-f3f5-f1f8-bb8a-616ce28ec9fb"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Slot", 100, "System", "electronics", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f0e954ff-4287-3d6e-c46d-4342f8480258"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Shein", 90, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f34fd8cc-dbf0-442c-3754-3fbe3cf484c5"), null, "Approved", false, "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "WorldRemit", 100, "System", "remittance", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f379da26-7f6c-bb2d-194f-623c6047d969"), null, "Approved", false, "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Oxfam", 100, "System", "donation", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f59b4566-c89b-fea0-1ca1-9e363b669c5d"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "MTN Nigeria", 100, "System", "phone", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f62d43a6-3daa-4dea-d115-1d8abe2e4304"), null, "Approved", false, "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Adobe", 100, "System", "software", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f776c243-15ac-8dd7-c38b-3a818df3ef9f"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Amazon.co.uk", 100, "System", "online", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f7b49a75-2e94-7633-d90c-617ae6e9d49e"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "KFC Nigeria", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f7db7fd4-ed10-c805-b2ac-3d79ee7a1b00"), null, "Approved", false, "education", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Ashesi", 100, "System", "tuition", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f7f5dbcc-686f-5346-337a-dab266531b02"), null, "Approved", false, "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "DSTV Kenya", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f8a6e4bf-bde2-ec9f-b294-1163738da9f2"), null, "Approved", false, "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Vanguard", 100, "System", "stocks", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f991619f-4cc7-5d68-48b6-3bb5eb497f49"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Bolt Ghana", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("f9d63877-e674-2129-ee6a-d93d71a2aa2f"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Kenya Airways", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fa429a77-b29a-f497-d4c0-125091a2cd97"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Dana Air", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fafc46a5-0528-c181-4d3b-c65b4fd18b88"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Sainsbury", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fb4634b5-7fd6-557f-8d3f-079c75035591"), null, "Approved", false, "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Zara", 100, "System", "clothing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fb870355-267d-eb24-e365-d94a7d4d3af9"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "National Rail", 100, "System", "public_transit", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fd68f74b-195d-eff8-a889-4155a57d9f47"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Papaye", 100, "System", "fast_food", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fe42d3bc-17c0-3786-ef4d-85ef28cda6e4"), null, "Approved", false, "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Yango Ghana", 100, "System", "ride_hailing", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fe480074-5db6-52d5-1cd0-2b1d21ce767d"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Waitrose", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fea48306-3b23-49e1-e33f-31bbfad7e97e"), null, "Approved", false, "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Aldi", 100, "System", "supermarket", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fef4e945-aaa1-ae83-5dde-27c672a457c0"), null, "Approved", false, "savings", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Cowrywise", 100, "System", null, new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("ff58b8c4-a618-9145-c7d8-400c1b6071b4"), null, "Approved", false, "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Buka", 100, "System", "restaurant", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fff76671-e4bc-874c-c862-0bca6fa7e939"), null, "Approved", false, "gifts", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, false, null, null, true, false, "contains", null, null, "Gift Card", 90, "System", "gift_card", new Guid("00000000-0000-0000-0000-000000000000"), null, null, new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("1187c214-6c2a-a5cb-eeda-c42742facb62"),
                columns: new[] { "IconName", "SortOrder" },
                values: new object[] { "money_off", 42 });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("1997cca4-34be-9af3-771e-cf07004c7e55"),
                column: "IconName",
                value: "call_received");

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("21c96e37-8556-81f9-5c95-f98c33dad96d"),
                columns: new[] { "IconName", "SortOrder" },
                values: new object[] { "school", 16 });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("332c5fff-59e9-295b-bd0e-fe96d65e09c1"),
                column: "IconName",
                value: "help_outline");

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("57f8c28b-6962-c2e3-291e-ce154606ddd9"),
                column: "IconName",
                value: "account_balance_wallet");

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("65b2c4a6-9984-256c-91af-4bda390f02fb"),
                columns: new[] { "IconName", "SortOrder" },
                values: new object[] { "spa", 21 });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("73ed47bb-8061-8683-b87b-076e643f2da0"),
                column: "IconName",
                value: "call_made");

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("a0f43bd8-78f3-1b9c-a030-4d44f7df6cd1"),
                columns: new[] { "IconName", "SortOrder" },
                values: new object[] { "account_balance", 43 });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("b4067e3f-8f67-675f-acbf-80778214a7c9"),
                column: "IconName",
                value: "movie");

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("e334f27f-6a6a-bc10-9d2e-2110f5b372ae"),
                columns: new[] { "IconName", "SortOrder" },
                values: new object[] { "flight", 32 });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("f05ac5d2-08b7-bc07-2044-44f3ad4d6173"),
                column: "IconName",
                value: "more_horiz");

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "TransactionCategories",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "DisplayName", "GroupName", "IconName", "IsActive", "IsDeleted", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("03e8c6c0-99f9-9902-0894-9dbacb9fa3e7"), "bills", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Bills", "Essentials", "receipt_long", true, false, 14, null, null },
                    { new Guid("1000865b-15ec-5b97-f63e-9fbc32d90e57"), "eating_out", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Eating Out", "Essentials", "restaurant", true, false, 12, null, null },
                    { new Guid("100fa15f-53fb-aa0b-68cd-487151fcb639"), "transport", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Transport", "Essentials", "directions_car", true, false, 13, null, null },
                    { new Guid("15600291-c8a8-3911-6661-08e7bf8923de"), "charity", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Charity", "Services", "volunteer_activism", true, false, 50, null, null },
                    { new Guid("2339d850-0f71-bb1f-816f-28326187cca0"), "gifts", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Gifts", "Shopping", "card_giftcard", true, false, 22, null, null },
                    { new Guid("2ffc3b49-9e83-9c51-a8d2-87f4f28856c3"), "investments", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Investments", "Financial", "trending_up", true, false, 41, null, null },
                    { new Guid("430ffdae-9825-2d78-ab96-f6f00d85a450"), "housing", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Housing", "Essentials", "home", true, false, 10, null, null },
                    { new Guid("7afa08c6-4354-361a-befb-4a58f450df11"), "health", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Health", "Essentials", "favorite", true, false, 15, null, null },
                    { new Guid("7b41d633-94d1-a620-b22d-e6d7819c47fe"), "pets", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Pets", "Lifestyle", "pets", true, false, 34, null, null },
                    { new Guid("80cd8204-35d9-b2de-3988-6646e17ab999"), "groceries", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Groceries", "Essentials", "shopping_cart", true, false, 11, null, null },
                    { new Guid("9a4144ec-2df3-43ce-c36b-0edc950dfdc8"), "fitness", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Fitness", "Lifestyle", "fitness_center", true, false, 33, null, null },
                    { new Guid("b3840cdd-dcb9-3182-bf05-be46fc2e96ae"), "subscriptions", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Subscriptions", "Lifestyle", "subscriptions", true, false, 31, null, null },
                    { new Guid("b3b7c5cd-936d-48d8-9397-193b770f4ab6"), "family_support", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Family Support", "Transfers", "family_restroom", true, false, 4, null, null },
                    { new Guid("ea2f4c5f-7021-e73f-f6b7-5bfa1004d450"), "savings", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Savings", "Financial", "savings", true, false, 40, null, null },
                    { new Guid("ebbecd37-00d9-ad88-51ad-e9c161f04b5a"), "shopping", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Shopping", "Shopping", "shopping_bag", true, false, 20, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategorisationRules_ScopeAware",
                schema: "dbo",
                table: "AnkCategorisationRules",
                columns: new[] { "Scope", "TenantId", "UserId", "IsActive", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CategorisationRules_ScopeAware",
                schema: "dbo",
                table: "AnkCategorisationRules");

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("007be53c-0dfa-2096-3545-074629fbb535"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("02edee53-3dfa-844d-b0cb-8b971d51da30"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("060c5ea6-db5a-cac8-23b4-fdc4933f2ff5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("0615b55c-5d6d-f812-c565-e12546db0d70"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("07b81b63-5ff1-e084-74c8-66eab183c12c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("08583478-5048-686c-5b79-48b13c3dd656"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("08a9c57a-90ac-15ab-c1b9-aacb0d2a26fd"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("0ab8417a-6cf9-d9cd-287e-5ee98fd3c8ea"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("0ad86bfd-959a-01f8-f838-70f46cbe4211"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("0b4e37a3-1634-20d2-0777-b7a32319199d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("0d2ed130-1736-b2d6-a30e-615198c71fb9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("0d578577-56d6-941d-c3a9-de333b3caa7d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("0ea50297-d89c-8357-718c-807a9713f9db"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("109124b2-4db4-9542-aa24-ce80e9fe2d9c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("10ab5979-cfc4-0c73-a2c9-3d9004a1272f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("10f6dcbe-8c3a-c31d-e173-92a3af60ec85"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1144e611-d4d0-cee0-70ef-68a0ed884526"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("11c62645-6844-a660-d2ef-66d9d9de62b6"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1203624d-aeb8-0e56-507a-92f4b0e380ca"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1262b5c8-2ca2-e0e3-4a26-81efbb785561"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1284d9d7-8a6f-7802-e325-6c28f832a7bb"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("13123ec1-d1b3-f786-ad82-29bd1d6740e3"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("13673659-286f-72b5-97c8-8d58707182df"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("142227a9-1e2e-a2f7-1f1b-98b0c4fb0e4d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("14504f0a-111d-541c-d4bb-11585c820ed1"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("14b648fd-cc76-9509-cbab-6241c4cc7363"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("14f049fe-165a-ab1d-4ce5-87eee3775998"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("15b44103-b0d0-e2bd-c5a8-a9dbcad5114a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1792c1ac-6dab-2880-b77c-0effe7834962"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("17e25fb5-cd3d-bbf2-d68c-174e8a3352d2"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1926bac4-b888-31b9-9e72-676c1d8a95b4"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("19d1b394-f98e-1ede-7fda-3ae77d7adbfe"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1a1b013a-6880-e269-ec16-477eb8d20c43"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1b372ef4-9eba-01bd-e7a9-fccff0519a76"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1b96586f-f941-ba60-6dec-e71fb7abcb00"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1c50fbef-5680-a983-4c67-7f765c66a011"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1c766908-b0fc-1b0d-d15e-c7e72a97f1c1"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1cabdf53-9a38-23b0-d1ba-9a851ccf6859"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1d6c441b-aa05-637b-e540-e8be92e5aa0a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1ee634c9-3688-afdf-04c2-0bb37266fea1"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("1f085df3-a282-c925-a17b-c6cb0749d07d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("20c97a56-7a3b-1679-9701-7c0a09d41e07"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("214636a3-5ce4-11a4-96d6-bbe742495693"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("214a14d9-70c5-a513-6c2f-15ed71c506bb"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("21a232bf-b185-6618-5761-0cc303376da8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2380d1e7-e476-7b00-64f4-0bf11bf41cd7"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("238f99d1-c16b-1de8-9751-9894d316f417"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("243e7cce-70bf-0112-5d8c-0b78d8ac887b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2498fcdc-63ef-3b0d-85e6-51646d31ca74"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("24c8e561-4bfb-f8f5-cf19-b7b88a461a38"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("25a44c45-3504-86b9-958c-baaf7ab1d482"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("25bab3e9-baab-2050-b7fc-aa25de93d8a8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("25e61c86-fa2f-8b94-f8ab-452212692a52"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("27b2f2f6-020f-1d7b-c806-d8b40fbbe819"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("27c8442a-6588-32fc-c074-ca806b2c2d97"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("27ee3f9e-206b-b7f7-d5c7-de6199445cdb"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("288408b4-3431-5931-1eff-158be195f967"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2a3033fb-b74d-b824-5f2d-2735b3d700d0"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2a585bfd-9cd5-ae05-c54c-f96cd7b4cdd8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2b0d9fdd-64ec-4c94-1b0a-b903ce81250d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2b233658-6811-83f7-9565-562de3a013eb"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2baf7028-9d03-3099-5e74-cd4766f5994e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2c3d20f8-f6ea-88b1-5d9e-c96648615a3e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2c5ba3bf-4b69-85cb-8624-838d08f758d9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2cde20ab-f874-992f-ba53-d71838913b18"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2d002bfe-e866-31e9-bb36-3f599fbcd885"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2dae7534-3d9a-3f45-f639-cae27c3e06af"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2ddb71af-1b00-5db9-6737-ae5132808bb5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("2e1ffa06-0a13-5fa5-24bc-7d90926cf299"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3084f92b-6cdc-cd75-767d-316f5c781d6c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("315d8328-8abd-5dd9-1bed-4a0bc644af6c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("31613adf-b097-235a-3913-bbcdaa574ef3"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("31c31e3d-506a-980f-5978-09a1e171f67f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("329d121d-f713-736c-ff87-c11ac7da6884"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("33770c5a-6e01-9f09-ef63-3bbd619ea2c6"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("339cdda1-a54b-3971-7abb-81b736ae7a2a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("347fe8cb-ad36-88eb-527d-452854dc716f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3520b2fb-f1e4-0911-226f-c76eecf69741"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("365bcb04-61d1-3b18-4f4a-b7d940ca0ffa"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("36b11130-7702-b31a-af71-3e85de2b6602"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("37649cc3-c5c3-fabb-f37f-8beb33fcbdf0"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("389434df-8de4-756d-4eb1-27491abe4928"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("39be3c69-c461-8ce1-5a7d-9f359c18c320"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3a233b55-6559-b079-a5a8-eaff86e88fad"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3a571dae-0fc6-d314-9125-b47a7f40bc60"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3a57c8c4-33d6-d80f-7d97-cb24ccbe94d5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3c93c35a-1e11-caa0-0d05-a2f7e54ae6b5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3d4388b5-70e7-d982-4f4a-db8d60480236"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3d4ba0fa-c87d-e95f-8ffb-aa79485b2e59"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3d6bf7ab-bcc2-6a49-82a9-b7be86e62bfe"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3d7ee515-59ae-4213-5ce4-96be5b61f1f4"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3d836b9d-9926-ef2b-0065-a7764e5ad1d7"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3dba8bdf-9c90-1e19-d600-3aa61af31016"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3e299cc5-acaf-8d20-f38e-ca01caf4479c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3f181020-30da-6641-9ff4-308fc1bac9e2"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("3f1e53c1-6ccd-9d90-ca76-b4e98ac8a665"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("40b6d7cb-202a-2b9a-a991-457dd3fc646e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("40b8a372-04e8-cf69-7e61-1709bb8cfd6b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4174154c-e645-5d18-9266-ab5353d44dc1"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("41894531-3785-6524-bdd9-0f2fe349e003"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("43e00cf2-f815-bfd1-cb69-54a8dd35bf21"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("44572c8d-e874-b4b4-3157-193793284468"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("470d03b5-14d7-927b-57e0-7222882e749e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4793ddad-d69a-2092-d667-cf48d24593df"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("47b2ba5f-97ba-4324-1ad8-64c42b421db5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("49091964-016e-61b4-65bc-7b9ff452883e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4926443d-b391-b9d8-fae1-546a764e482c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4978bfc9-8e4d-1e26-edb1-44c9300d6aef"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("49a7bdaa-6b3f-6acc-7d06-3152c3a0f431"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4a0811d6-320b-02ec-138d-fd2855b584aa"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4a2c331c-e8f1-10d5-ea04-f3ce62a01114"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4cdb4d57-fd88-93b8-f40a-3475f69c36c4"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4d4fa736-6bbc-e3b3-726f-238a2410f4d9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4d97510e-bf64-be4c-2a76-9bd3fe44ee3c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4dc0dd32-28b2-7a7b-c407-300c9581fb8b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4dd1d566-48d1-10bc-6fd7-4426e76283fd"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4f28b2ff-0551-c8d9-db9a-7f539b0302e8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("4fefd6e4-085f-8521-2855-c9b0a2c21d4b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("506e3d6f-67b4-fea1-7ad3-82960744f2db"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("52f0ff1b-dc94-2c8a-9b88-a23191763864"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("558c09c4-f4fb-c077-4405-e02f08dc697d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("55f0a918-6e30-b290-04bc-09b7fe68a39b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("575fd074-581c-684e-3838-31f042c8edde"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5779fea0-2797-1a36-67e8-7af773772aab"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5955c656-1833-9322-b1e0-c64f2081bc10"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5a4ecb71-120c-154a-c7e8-ced630690af3"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5aa34361-e2aa-36af-9970-f71a3188ee70"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5af92690-1a25-bcda-42be-d5e08c733604"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5b60bc26-0fc6-ba2a-77da-fd7f1c75218f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5dad92d9-82ab-0422-d1af-3a35df9bd8d8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5df9257e-8a52-c5ab-b0cd-a9ae30f28f3c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5e2ef72f-7dbd-0865-8798-3894ab649b2b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5e78aee0-6158-2a90-bb96-5d43bbb5f47a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("5ea4a475-df51-0eb4-0351-b25ca2e7c7c5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("6042c612-fd17-77ba-30d0-3d8d5795d84f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("61bcc1ce-7d5c-8783-b7c2-3e350f25c3ef"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("62bb8eae-ed68-b250-c7c2-2b6ccebd3c0f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("62d4aa77-e7fd-3261-fa87-2f3fbe94a7d0"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("62e05bc9-01e2-19f4-3f23-52887630a2e7"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("63341952-970f-ed05-db7e-5c6299fb434f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("6529f2e0-0e40-f882-33d9-9e1fc20fe503"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("67739ecc-a8f8-6e39-b405-c0c47aaf5fe5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("677af6da-dde8-191c-3231-8e5d30298957"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("68e00c9b-b16f-c643-8e42-99d19d4d5db5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("6924ad96-5eac-9345-ec08-6f34f12b8c03"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("69abfe80-7e2f-aa9b-e49e-91c3e711e73d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("69d62e03-c729-bece-40bb-fac41a1fd9d8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("6a41182e-dbf0-6a33-22b8-394697d222ea"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("6bc85ecd-45f5-ec1a-6cf6-5c4f52bf2799"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("6c6b281b-05f8-19df-0b62-cc82ea721f6b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("6cc322f1-9f65-09b0-a444-01987d79f4a4"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("6daf1878-de96-0422-3343-5b533caa00cd"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("6e2b7937-756e-64b7-82bc-c90ce49a2953"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("6fd7554e-27ee-f44e-0ccb-55cff9bf9e3a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("703383dd-afe5-d4ba-1de3-ff5dca5936cd"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("709c0b9b-03a9-a187-3361-21596979ec9e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("730f8209-5719-76f4-37ca-48fdb38f40c6"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("73e00c89-4b02-5cdb-484f-becaba8d161e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("752b75e5-3013-f935-57ba-65f98aefc7a6"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("79c45e5a-2c01-97df-ab83-69b760fe4f70"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("7daf1e52-b01d-05fc-4334-4e782ada9021"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("7e38a699-ccb9-f753-6151-0223d57a2d4e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8030157b-2a4d-f73d-eb72-3a8ca318e805"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("817f25be-3625-0907-e92f-f5b0777f592c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("81fc0724-54bb-8b7e-3268-ab2e10a84d8b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("82e73bcd-94bc-c4ac-2e33-f716b242cdfe"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("83340abd-9804-f4b6-f8d9-ffc037b90e3f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("83e0a9fc-8424-c69b-e1ac-5d1d0098111a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8468c179-47e1-124e-b9b4-37e3840d1fc4"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8529ad67-078b-7625-40cf-02bf3b3e9ab5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("86b44be6-ad6b-51a3-0a1b-f39aab98c6a9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("86d8cd0f-4d6e-5b41-5939-24cd1e590fdc"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("878ddd55-1b01-6cfa-e647-3d736d41b941"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("87a5a291-f6df-bf2f-a9d6-34acc8ee4303"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("88598591-7572-50a5-2b0d-305ef14228e5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8864a762-0e39-3eba-7994-5a53533970d8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8923f640-34cd-c0e9-6585-4e6195e0c3f0"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8942d49d-440f-476f-9e0d-5ad13c92d31a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8a8db70c-0705-9bf4-afe6-c31c79ae44bf"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8ae66331-e65d-a917-475b-e7600ab59463"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8b548b64-5645-1a7f-5ba2-96953a1c569e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8b76873b-ea55-74e4-c32d-bffc82f372cf"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8bb33c49-edf3-bf54-9daf-7084e335fb56"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8ce14935-0bdd-3089-2539-48e80be6cdb4"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8db85ddf-116b-5fcf-26e1-851b6c1b9cc1"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8f8c6a2c-43d8-55af-19b2-1bf283e3b615"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("8fc144af-86ad-64eb-cc07-e28b85c7f6ff"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("91e7175c-ae95-5e58-d701-dc178b55eaae"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("92556850-85c0-3ca3-c05a-9a542a3449c9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("92b231fd-b634-c750-983a-af8486d35cd8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9314f400-5f9a-3814-f147-527e85775ab0"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("95c47ff7-cf1e-940e-3a26-02e70e689349"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("96db17fe-2720-933f-aef0-862cc4f57789"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("970f629f-051c-4599-f713-2e77c2a5741f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("98486149-3de5-c9cc-b8c4-6b5104cf135c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("98597aae-5a4f-80f2-6b83-eea2a5a5c91b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("98860d54-2db5-cec4-eec2-fd4b962518d2"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("99948e67-7b93-b4b5-0ee1-2b311f08747b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9a093889-105b-6128-20de-0c08f2c249a9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9a11044a-2b06-ec27-df4e-6a8fbc2f5a22"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9a50b8a4-f4b7-79bc-a3ee-35e980ae718e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9ab13ed7-39c1-835a-3349-2e98ed79e42c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9aeb73aa-60b0-f622-127a-0715ac328d62"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9b7a5557-d535-7659-7a65-39ce19d2180d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9b7ac41b-475c-e28a-b4ae-2a5e57cfa6a9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9b92eb36-b723-d281-643b-c1b74cc091fd"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9c5c5ca0-58c5-9c0b-2ed4-b816cfaa480a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9c9c4854-d3fa-8a80-2495-c08ef79c9026"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9cc1c74b-7ece-b6bf-8954-88384ea7f0d0"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9ebc5207-8c4d-858e-01f2-bc5a1ac231d1"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9ec7a5ea-d311-026f-e313-1b80469c5487"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("9f4bf3d4-db27-73b4-4372-49abee1145ec"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a0157292-287b-5b6e-59ca-de475839b819"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a072e78a-c458-911f-f707-feba5bd904f4"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a14aa52f-e717-604f-d45c-0c6cdaabac2e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a15919be-1396-9585-989a-2dec432c455c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a1808d71-b29e-8b2d-0d71-143325b85f64"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a185907c-ab58-5956-877a-30de20282c9a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a535dd7e-0f55-87ab-cea7-d80602a15d1d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a6f7497c-1c04-615a-280c-cdfbadacfa43"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a7d79f9b-8654-95a8-b5a2-8e69d4f539bf"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a8587738-3a7f-2dcf-02da-3f1faae19980"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a8bcf3ef-2669-aad7-a638-f8c0435668af"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a8cf30c2-e287-2945-dc6f-7456a292770c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a927a565-a190-6892-0330-5144e1458a05"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("a95d7231-365c-6ecf-7ade-2e4f39591b90"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("aa414c39-8705-1375-0f6d-cedccc8acd01"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("aa6613ba-cefd-f34a-ea65-c24565a068ee"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("ab03dbb3-493a-bbb4-fbf7-f5f7326b1016"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("aca69a6c-aac9-c5a7-01d1-cf209840cd4a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("adb51488-ef35-b8d4-c274-9cca7f7b2ef4"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b03e1ad3-9bb2-76b7-17d9-4b21269f40e3"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b1a4de04-ef5e-1cf2-fe7d-82f175e235b0"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b1b2dd5b-174c-81b8-b50e-2fb0554ec429"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b377af45-63b0-1997-91c5-ba93f90b8ef2"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b4593d3e-3dc4-8e4c-492a-0da325c7e9ad"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b5b4f64d-0780-2224-2a91-6223028a53d9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b63e2211-fca2-1068-30da-6e1151036f31"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b7b6c768-76ca-f334-44d4-eef1f1c69f71"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b86ad24f-998d-8152-764c-734d7adecc4b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b8dd92b4-bb3a-9004-cc50-1d51a4d3ce97"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b94f8ee1-fffb-18a3-7790-c0482f321eea"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("b9d09e71-0d2b-a433-65a5-bc5bfd2a7eb9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("ba835fc2-eebb-2ebb-50fc-9be93e30109e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("bb42de6d-7124-8485-49c8-782ef0fca3ad"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("bbd28d80-dba9-d0ef-2e9d-9857d0c839e5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("bcc1b680-d694-558d-d44e-36cec4588941"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("bd3ed9c2-b56c-73f3-04a3-3430825fb8e5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("bdf26eab-d17c-b089-0df1-450076468323"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("be6f1d3c-3a3f-55c8-d458-05dfe1d3919f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("bf8cdcc5-541a-29ae-5305-ec0e29e98878"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c02d6f93-6fe1-d602-ede3-4ca9f203c568"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c090db3d-91f1-baeb-6b6c-e4779a7558a3"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c0b89dbe-3c14-7851-f80f-4923d269efe3"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c170d2e6-5365-bdd1-d82a-1756765ea71e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c17dd3d1-c9ff-1bea-35b9-fca42665b98a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c1e68af4-aecd-b08c-1f2a-a4a726934d46"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c214e6c6-b18d-7836-7c68-7cb96362b31f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c4e68fd0-a5ea-1d6f-0b71-058afbc9ade6"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c53e9f62-7e79-d045-812b-a5703dac3c08"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c6b4f046-4cd6-67da-eafd-76e2afcda05b"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c6b87f54-ad07-a8b4-bc1a-0f71d51e886e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c71d5d1d-b93a-6a01-ec3a-13230697828d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c78f322c-868e-7ca8-d185-d9d08db967d5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c7bb37ad-b6b5-6b25-6480-8fd83501817f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c7f31def-6c64-b317-5684-6e8d9167387c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c8a97f74-5706-fb6d-1bd5-9f05247f22e7"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c93ebefe-ae03-8418-d5d1-c80c474db05f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c9c1a562-b685-e750-0ace-1e86a7dc0891"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("c9d38686-1be0-05c8-8a1f-1aec05484f94"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("cade5b35-62aa-00da-d96f-5aaf46b69f60"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("cae67098-cfb5-d528-1ba4-c72bab5bd014"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("cae80a91-77a6-b9ed-f2e4-0cbe2d4e3970"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("cb3cd92e-a1d0-00a9-971c-0f7e0b3d349f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("cd15110c-6519-671f-3cf3-bbad0db6ad06"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("cd94ea76-1217-d7ce-f31c-e10378c418ae"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("cd9fb1db-b47c-79a5-9875-0eeebbe7bd89"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("ce041c66-d307-49b5-0974-9f2b0780685e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("cf40c4bd-5883-7f68-0180-fd782e84318f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d229e8ee-2457-d215-d875-9da243d1dcd5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d2854f4f-8da9-980a-5f1e-c854ae8d37b8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d2b55aef-d43f-138a-b462-600516b8ca99"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d2b8887b-68c5-b038-1624-123dce7bdd2e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d4116d58-6628-398c-99c6-9736cacc0f70"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d4de5b0a-c321-bfc9-5c1e-d3bfebce7acf"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d5a0a961-48b6-12ab-bd72-f47b92b7f605"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d6ac4184-f710-161c-0fa9-ce3f4187c086"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d718932e-3ea9-cc45-99cc-fc8e79fe085f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d82c0c18-649f-86b5-3154-a2e3a5355e41"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d90256d9-f693-4bf7-f3e3-2014541d292f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("d9043e88-0860-90ca-6ddf-0f4b2c167675"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("dccf84e3-fdb4-10d3-39ee-ce67be264d3a"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("dd2136cc-9f43-e5ba-0283-3c199a25ecb3"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("dd6cec94-333c-cc0b-d040-4def7a6d03f2"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("dd73c564-6956-ba04-d049-703c2662d47c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("df139fa9-0cee-b864-6ee8-f180bddccf63"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("dfec493f-a528-9277-096c-1facffc5653c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("dff98fc8-0305-6a82-15b5-e3e4177ad72c"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("e0199745-19d9-5e4c-e536-4f98ccd54fe6"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("e01ee21f-2bad-116f-6eef-8cfd652070a1"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("e2435fbb-838a-2756-f0d6-56ea43172b83"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("e3546581-25fc-0c7c-f322-822b622d7ab9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("e4b61e17-2616-61d7-bbbb-68acc79ddd44"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("e658c66f-8647-0377-6d02-8774af2d3e77"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("e6c45a12-4925-e66f-6534-c31f9c24bb82"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("e813d5a7-66f6-d007-bc2f-a13839693dc8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("eb102201-4f40-239a-c95c-1ddae578f0a6"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("eb89efc0-d77f-1156-8fa7-4d089b93caf9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("ebad1dd6-e76e-b07c-a880-a6a1967333f9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("ec797db5-9afd-f1b9-8d32-2468769d34dd"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("ece344a6-b5d8-8708-d9ce-095f4772784f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("ed5c70c7-e763-283a-fda8-47f80a8309d2"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("ed9aee1e-6262-3094-53e5-2566d073d7c5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("eec8856d-35ef-2a82-4434-f15902df5f67"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("eed6c1d3-2d3a-04da-2952-d8f2c4123469"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("eee5f1b2-1c79-3bb2-4767-caf08c0a9697"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("efafa623-6c2f-c2e1-304d-e1a4ffdac625"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("effa9f3f-833e-a3fc-6b4c-d9a94742c922"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f0388474-f3f5-f1f8-bb8a-616ce28ec9fb"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f0e954ff-4287-3d6e-c46d-4342f8480258"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f34fd8cc-dbf0-442c-3754-3fbe3cf484c5"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f379da26-7f6c-bb2d-194f-623c6047d969"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f59b4566-c89b-fea0-1ca1-9e363b669c5d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f62d43a6-3daa-4dea-d115-1d8abe2e4304"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f776c243-15ac-8dd7-c38b-3a818df3ef9f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f7b49a75-2e94-7633-d90c-617ae6e9d49e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f7db7fd4-ed10-c805-b2ac-3d79ee7a1b00"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f7f5dbcc-686f-5346-337a-dab266531b02"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f8a6e4bf-bde2-ec9f-b294-1163738da9f2"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f991619f-4cc7-5d68-48b6-3bb5eb497f49"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("f9d63877-e674-2129-ee6a-d93d71a2aa2f"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("fa429a77-b29a-f497-d4c0-125091a2cd97"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("fafc46a5-0528-c181-4d3b-c65b4fd18b88"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("fb4634b5-7fd6-557f-8d3f-079c75035591"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("fb870355-267d-eb24-e365-d94a7d4d3af9"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("fd68f74b-195d-eff8-a889-4155a57d9f47"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("fe42d3bc-17c0-3786-ef4d-85ef28cda6e4"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("fe480074-5db6-52d5-1cd0-2b1d21ce767d"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("fea48306-3b23-49e1-e33f-31bbfad7e97e"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("fef4e945-aaa1-ae83-5dde-27c672a457c0"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("ff58b8c4-a618-9145-c7d8-400c1b6071b4"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "AnkCategorisationRules",
                keyColumn: "Id",
                keyValue: new Guid("fff76671-e4bc-874c-c862-0bca6fa7e939"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("03e8c6c0-99f9-9902-0894-9dbacb9fa3e7"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("1000865b-15ec-5b97-f63e-9fbc32d90e57"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("100fa15f-53fb-aa0b-68cd-487151fcb639"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("15600291-c8a8-3911-6661-08e7bf8923de"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("2339d850-0f71-bb1f-816f-28326187cca0"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("2ffc3b49-9e83-9c51-a8d2-87f4f28856c3"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("430ffdae-9825-2d78-ab96-f6f00d85a450"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("7afa08c6-4354-361a-befb-4a58f450df11"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("7b41d633-94d1-a620-b22d-e6d7819c47fe"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("80cd8204-35d9-b2de-3988-6646e17ab999"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("9a4144ec-2df3-43ce-c36b-0edc950dfdc8"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("b3840cdd-dcb9-3182-bf05-be46fc2e96ae"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("b3b7c5cd-936d-48d8-9397-193b770f4ab6"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("ea2f4c5f-7021-e73f-f6b7-5bfa1004d450"));

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("ebbecd37-00d9-ad88-51ad-e9c161f04b5a"));

            migrationBuilder.DropColumn(
                name: "SubCategory",
                schema: "dbo",
                table: "AnkCategorisationRules");

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("1187c214-6c2a-a5cb-eeda-c42742facb62"),
                columns: new[] { "IconName", "SortOrder" },
                values: new object[] { null, 50 });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("1997cca4-34be-9af3-771e-cf07004c7e55"),
                column: "IconName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("21c96e37-8556-81f9-5c95-f98c33dad96d"),
                columns: new[] { "IconName", "SortOrder" },
                values: new object[] { null, 14 });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("332c5fff-59e9-295b-bd0e-fe96d65e09c1"),
                column: "IconName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("57f8c28b-6962-c2e3-291e-ce154606ddd9"),
                column: "IconName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("65b2c4a6-9984-256c-91af-4bda390f02fb"),
                columns: new[] { "IconName", "SortOrder" },
                values: new object[] { null, 22 });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("73ed47bb-8061-8683-b87b-076e643f2da0"),
                column: "IconName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("a0f43bd8-78f3-1b9c-a030-4d44f7df6cd1"),
                columns: new[] { "IconName", "SortOrder" },
                values: new object[] { null, 51 });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("b4067e3f-8f67-675f-acbf-80778214a7c9"),
                column: "IconName",
                value: null);

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("e334f27f-6a6a-bc10-9d2e-2110f5b372ae"),
                columns: new[] { "IconName", "SortOrder" },
                values: new object[] { null, 31 });

            migrationBuilder.UpdateData(
                schema: "dbo",
                table: "TransactionCategories",
                keyColumn: "Id",
                keyValue: new Guid("f05ac5d2-08b7-bc07-2044-44f3ad4d6173"),
                column: "IconName",
                value: null);

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "TransactionCategories",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "DisplayName", "GroupName", "IconName", "IsActive", "IsDeleted", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("1bee5ee1-7f45-0bd0-5637-2f0a8016bc33"), "home_improvement", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Home Improvement", "Shopping", null, true, false, 21, null, null },
                    { new Guid("27dabf45-052c-8784-2620-6c70523d0f64"), "transportation", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Transportation", "Essentials", null, true, false, 12, null, null },
                    { new Guid("2ab4a16d-91d7-c1d0-25ff-69715bdecded"), "medical", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Medical", "Essentials", null, true, false, 13, null, null },
                    { new Guid("49de988e-ecea-4a92-bffc-95622e79be91"), "general_merchandise", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "General Merchandise", "Shopping", null, true, false, 20, null, null },
                    { new Guid("70fd4eec-36ef-e460-ccf0-6361d3bb517b"), "general_services", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "General Services", "Services", null, true, false, 40, null, null },
                    { new Guid("b7981080-c87f-d631-1313-6a16c3b4f063"), "rent_and_utilities", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Rent & Utilities", "Essentials", null, true, false, 11, null, null },
                    { new Guid("cda09b4b-2680-24f9-273e-2379bb687a3b"), "food_and_drink", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Food & Drink", "Essentials", null, true, false, 10, null, null },
                    { new Guid("e80f8dd4-d1a0-8d96-24f5-4954356423ac"), "government_and_non_profit", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Government & Non-Profit", "Services", null, true, false, 41, null, null }
                });
        }
    }
}
