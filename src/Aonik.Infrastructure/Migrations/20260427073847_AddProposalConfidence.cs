using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aonik.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalConfidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Confidence",
                schema: "dbo",
                table: "AnkProposals",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.85m);

            // Backfill: derive Confidence from RiskTier so existing rows aren't
            // all flattened to the column default. Mirrors the mapping that
            // AgentProposalQueryService used before this column existed.
            migrationBuilder.Sql(@"
                UPDATE [dbo].[AnkProposals]
                SET [Confidence] = CASE LOWER([RiskTier])
                    WHEN 'low'    THEN 0.9500
                    WHEN 'medium' THEN 0.8500
                    WHEN 'high'   THEN 0.7000
                    ELSE                0.8000
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confidence",
                schema: "dbo",
                table: "AnkProposals");
        }
    }
}
