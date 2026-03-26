using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FileExtractionS3Pipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "AIModels",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "AgentFiles");

            migrationBuilder.DropColumn(
                name: "SummarizedText",
                table: "AgentFiles");

            migrationBuilder.AddColumn<int>(
                name: "ExtractionVersion",
                table: "AgentFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "S3ChunksKey",
                table: "AgentFiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "S3NormalizedTextKey",
                table: "AgentFiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "S3RawTextKey",
                table: "AgentFiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemAdminInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvitedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Token = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAdminInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemAdminInvitations_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemAdminInvitations_InvitedByUserId",
                table: "SystemAdminInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAdminInvitations_Token",
                table: "SystemAdminInvitations",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemAdminInvitations");

            migrationBuilder.DropColumn(
                name: "ExtractionVersion",
                table: "AgentFiles");

            migrationBuilder.DropColumn(
                name: "S3ChunksKey",
                table: "AgentFiles");

            migrationBuilder.DropColumn(
                name: "S3NormalizedTextKey",
                table: "AgentFiles");

            migrationBuilder.DropColumn(
                name: "S3RawTextKey",
                table: "AgentFiles");

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "AgentFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SummarizedText",
                table: "AgentFiles",
                type: "text",
                nullable: true);

            migrationBuilder.InsertData(
                table: "AIModels",
                columns: new[] { "Id", "CreatedAt", "DisplayName", "InputCostPer1M", "IsActive", "IsDefault", "ModelId", "Name", "OutputCostPer1M", "Provider", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc), "GPT-4o Mini", 0.15m, true, false, "gpt-4o-mini", "gpt-4o-mini", 0.60m, "OpenAi", new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Gemini 2.0 Flash", 0.075m, true, false, "gemini-2.0-flash", "gemini-2.0-flash", 0.30m, "Gemini", new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Amazon Nova Lite", 0.06m, true, false, "amazon.nova-lite-v1:0", "amazon-nova-lite", 0.24m, "Bedrock", new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "CreatedAt", "Description", "HasWhatsApp", "IsActive", "MaxAgents", "MonthlyPriceUsd", "Name", "Slug", "StripePriceId", "TrialDays", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Up to 2 agents for small teams", false, true, 2, 29.00m, "Starter", "starter", null, 7, new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Up to 5 agents for growing teams", false, true, 5, 79.00m, "Professional", "professional", null, 7, new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Up to 10 agents with WhatsApp integration", true, true, 10, 199.00m, "Scale", "scale", null, 7, new DateTime(2026, 3, 20, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }
    }
}
