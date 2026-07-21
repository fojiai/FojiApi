using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentWhatsAppAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WhatsAppAccessTokenEncrypted",
                table: "Agents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WhatsAppAccessTokenEncrypted",
                table: "Agents");
        }
    }
}
