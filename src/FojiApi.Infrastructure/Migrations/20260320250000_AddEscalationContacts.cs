using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEscalationContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Agent: escalation contact fields ─────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "SupportWhatsAppNumber",
                table: "Agents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesWhatsAppNumber",
                table: "Agents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportEmail",
                table: "Agents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesEmail",
                table: "Agents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // ── Plan: escalation contacts feature flag ────────────────────────
            migrationBuilder.AddColumn<bool>(
                name: "HasEscalationContacts",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Enable for Professional (Id=2) and Scale (Id=3) plans
            migrationBuilder.Sql("""
                UPDATE "Plans"
                SET "HasEscalationContacts" = TRUE
                WHERE "Id" IN (2, 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SupportWhatsAppNumber", table: "Agents");
            migrationBuilder.DropColumn(name: "SalesWhatsAppNumber", table: "Agents");
            migrationBuilder.DropColumn(name: "SupportEmail", table: "Agents");
            migrationBuilder.DropColumn(name: "SalesEmail", table: "Agents");
            migrationBuilder.DropColumn(name: "HasEscalationContacts", table: "Plans");
        }
    }
}
