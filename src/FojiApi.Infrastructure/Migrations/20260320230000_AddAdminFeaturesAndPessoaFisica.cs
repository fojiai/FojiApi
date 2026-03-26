using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminFeaturesAndPessoaFisica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Company: Pessoa Física / Jurídica support + admin notes ───────

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "Companies",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Business");

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
                name: "AdminNotes",
                table: "Companies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            // ── Plan: private/custom plan support ────────────────────────────

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomForCompanyId",
                table: "Plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Plans_Companies_CustomForCompanyId",
                table: "Plans",
                column: "CustomForCompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_Plans_CustomForCompanyId",
                table: "Plans",
                column: "CustomForCompanyId");

            // ── Subscription: admin-assigned subscriptions ───────────────────

            migrationBuilder.AddColumn<int>(
                name: "AssignedByAdminId",
                table: "Subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "Subscriptions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Users_AssignedByAdminId",
                table: "Subscriptions",
                column: "AssignedByAdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_AssignedByAdminId",
                table: "Subscriptions",
                column: "AssignedByAdminId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Users_AssignedByAdminId",
                table: "Subscriptions");
            migrationBuilder.DropIndex(name: "IX_Subscriptions_AssignedByAdminId", table: "Subscriptions");
            migrationBuilder.DropColumn(name: "AssignedByAdminId", table: "Subscriptions");
            migrationBuilder.DropColumn(name: "AdminNotes", table: "Subscriptions");

            migrationBuilder.DropForeignKey(name: "FK_Plans_Companies_CustomForCompanyId", table: "Plans");
            migrationBuilder.DropIndex(name: "IX_Plans_CustomForCompanyId", table: "Plans");
            migrationBuilder.DropColumn(name: "CustomForCompanyId", table: "Plans");
            migrationBuilder.DropColumn(name: "IsPublic", table: "Plans");

            migrationBuilder.DropColumn(name: "AccountType", table: "Companies");
            migrationBuilder.DropColumn(name: "CpfCnpj", table: "Companies");
            migrationBuilder.DropColumn(name: "TradeName", table: "Companies");
            migrationBuilder.DropColumn(name: "AdminNotes", table: "Companies");
        }
    }
}
