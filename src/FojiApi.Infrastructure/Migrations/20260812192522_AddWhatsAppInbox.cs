using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WhatsAppDisplayName",
                table: "UserCompanies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppMode",
                table: "Agents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "WhatsAppConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    AgentId = table.Column<int>(type: "integer", nullable: false),
                    PhoneNumberId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContactWaId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactId = table.Column<int>(type: "integer", nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastInboundAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppConversations_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WhatsAppConversations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppConversations_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    WamId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SentByUserId = table.Column<int>(type: "integer", nullable: true),
                    SenderDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppMessages_Users_SentByUserId",
                        column: x => x.SentByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WhatsAppMessages_WhatsAppConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "WhatsAppConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_AgentId_ContactWaId",
                table: "WhatsAppConversations",
                columns: new[] { "AgentId", "ContactWaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_CompanyId_LastMessageAt",
                table: "WhatsAppConversations",
                columns: new[] { "CompanyId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversations_ContactId",
                table: "WhatsAppConversations",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_ConversationId_CreatedAt",
                table: "WhatsAppMessages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_SentByUserId",
                table: "WhatsAppMessages",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_WamId",
                table: "WhatsAppMessages",
                column: "WamId",
                unique: true,
                filter: "\"WamId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppMessages");

            migrationBuilder.DropTable(
                name: "WhatsAppConversations");

            migrationBuilder.DropColumn(
                name: "WhatsAppDisplayName",
                table: "UserCompanies");

            migrationBuilder.DropColumn(
                name: "WhatsAppMode",
                table: "Agents");
        }
    }
}
