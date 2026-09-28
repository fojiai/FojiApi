using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxConversationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "WhatsAppConversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ResolvedAutomatically",
                table: "WhatsAppConversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "WhatsAppConversations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_CompanyId_Status_LastMessageAt",
                table: "WhatsAppConversations",
                columns: new[] { "CompanyId", "Status", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_Status_LastMessageAt",
                table: "WhatsAppConversations",
                columns: new[] { "Status", "LastMessageAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppConversations_CompanyId_Status_LastMessageAt",
                table: "WhatsAppConversations");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppConversations_Status_LastMessageAt",
                table: "WhatsAppConversations");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "WhatsAppConversations");

            migrationBuilder.DropColumn(
                name: "ResolvedAutomatically",
                table: "WhatsAppConversations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "WhatsAppConversations");
        }
    }
}
