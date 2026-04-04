using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentWidgetCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConversationStarters",
                table: "Agents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WelcomeMessage",
                table: "Agents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WidgetPlaceholder",
                table: "Agents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WidgetPosition",
                table: "Agents",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WidgetPrimaryColor",
                table: "Agents",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WidgetTitle",
                table: "Agents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConversationStarters",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "WelcomeMessage",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "WidgetPlaceholder",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "WidgetPosition",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "WidgetPrimaryColor",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "WidgetTitle",
                table: "Agents");
        }
    }
}
