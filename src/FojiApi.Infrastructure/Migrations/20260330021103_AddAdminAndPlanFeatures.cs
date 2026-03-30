using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAndPlanFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "Subscriptions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedByAdminId",
                table: "Subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomForCompanyId",
                table: "Plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasEscalationContacts",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxConversationsPerMonth",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxMessagesPerMonth",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "Companies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Business");

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "Companies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CpfCnpj",
                table: "Companies",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeName",
                table: "Companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesEmail",
                table: "Agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesWhatsAppNumber",
                table: "Agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportEmail",
                table: "Agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportWhatsAppNumber",
                table: "Agents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    StatDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sessions = table.Column<int>(type: "integer", nullable: false),
                    Messages = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyStats_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_AssignedByAdminId",
                table: "Subscriptions",
                column: "AssignedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_CustomForCompanyId",
                table: "Plans",
                column: "CustomForCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyStats_CompanyId_StatDate",
                table: "DailyStats",
                columns: new[] { "CompanyId", "StatDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Plans_Companies_CustomForCompanyId",
                table: "Plans",
                column: "CustomForCompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Users_AssignedByAdminId",
                table: "Subscriptions",
                column: "AssignedByAdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Plans_Companies_CustomForCompanyId",
                table: "Plans");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Users_AssignedByAdminId",
                table: "Subscriptions");

            migrationBuilder.DropTable(
                name: "DailyStats");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_AssignedByAdminId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Plans_CustomForCompanyId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "AssignedByAdminId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CustomForCompanyId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "HasEscalationContacts",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MaxConversationsPerMonth",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MaxMessagesPerMonth",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CpfCnpj",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TradeName",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SalesEmail",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "SalesWhatsAppNumber",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "SupportEmail",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "SupportWhatsAppNumber",
                table: "Agents");
        }
    }
}
