using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppHybridMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAiGenerated",
                table: "WhatsAppMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HumanTakeover",
                table: "WhatsAppConversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TakeoverAt",
                table: "WhatsAppConversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TakeoverByUserId",
                table: "WhatsAppConversations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TakeoverReason",
                table: "WhatsAppConversations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAiGenerated",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "HumanTakeover",
                table: "WhatsAppConversations");

            migrationBuilder.DropColumn(
                name: "TakeoverAt",
                table: "WhatsAppConversations");

            migrationBuilder.DropColumn(
                name: "TakeoverByUserId",
                table: "WhatsAppConversations");

            migrationBuilder.DropColumn(
                name: "TakeoverReason",
                table: "WhatsAppConversations");
        }
    }
}
