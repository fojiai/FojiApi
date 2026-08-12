using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxMediaAndAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MediaContentType",
                table: "WhatsAppMessages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaFileName",
                table: "WhatsAppMessages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaS3Key",
                table: "WhatsAppMessages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "WhatsAppMessages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AssignedUserId",
                table: "WhatsAppConversations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_AssignedUserId",
                table: "WhatsAppConversations",
                column: "AssignedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_WhatsAppConversations_Users_AssignedUserId",
                table: "WhatsAppConversations",
                column: "AssignedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WhatsAppConversations_Users_AssignedUserId",
                table: "WhatsAppConversations");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppConversations_AssignedUserId",
                table: "WhatsAppConversations");

            migrationBuilder.DropColumn(
                name: "MediaContentType",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MediaFileName",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MediaS3Key",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "WhatsAppConversations");
        }
    }
}
