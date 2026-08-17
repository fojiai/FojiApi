using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppOveragePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WhatsAppOverageCentavos",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Deploying the meter must not switch WhatsApp off for anyone who
            // already has it. Existing WhatsApp plans become uncapped (-1), which
            // preserves today's behaviour exactly; real allowances are then set
            // deliberately from the admin panel rather than by a guess in a
            // migration.
            migrationBuilder.Sql("""
                UPDATE "Plans"
                SET "WhatsAppMessagesPerMonth" = -1
                WHERE "HasWhatsApp" = TRUE AND "WhatsAppMessagesPerMonth" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WhatsAppOverageCentavos",
                table: "Plans");
        }
    }
}
