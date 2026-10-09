using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SwitchBillingToAsaas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_CompanyId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "StripePriceId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Companies");

            migrationBuilder.AddColumn<string>(
                name: "AsaasSubscriptionId",
                table: "Subscriptions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CancelAtPeriodEnd",
                table: "Subscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CardBrand",
                table: "Subscriptions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardLast4",
                table: "Subscriptions",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardToken",
                table: "Subscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cycle",
                table: "Subscriptions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<DateTime>(
                name: "PastDueSince",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Subscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingPlanId",
                table: "Subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Subscriptions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Plans",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "BRL",
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldDefaultValue: "USD");

            migrationBuilder.AddColumn<decimal>(
                name: "YearlyPrice",
                table: "Plans",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsaasCustomerId",
                table: "Companies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillingCheckouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    Cycle = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ReplacesSubscriptionId = table.Column<int>(type: "integer", nullable: true),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AsaasCheckoutId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AsaasSubscriptionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AsaasPaymentId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingCheckouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingCheckouts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillingCheckouts_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillingPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AsaasPaymentId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    BillingType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvoiceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NfseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    NotifiedCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotifiedOverdueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingPayments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillingPayments_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BillingWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Event = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_AsaasSubscriptionId",
                table: "Subscriptions",
                column: "AsaasSubscriptionId",
                unique: true,
                filter: "\"AsaasSubscriptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_CompanyId_Status",
                table: "Subscriptions",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PendingPlanId",
                table: "Subscriptions",
                column: "PendingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingCheckouts_AsaasCheckoutId",
                table: "BillingCheckouts",
                column: "AsaasCheckoutId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingCheckouts_AsaasSubscriptionId",
                table: "BillingCheckouts",
                column: "AsaasSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingCheckouts_CompanyId_Status",
                table: "BillingCheckouts",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingCheckouts_PlanId",
                table: "BillingCheckouts",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingPayments_AsaasPaymentId",
                table: "BillingPayments",
                column: "AsaasPaymentId",
                unique: true,
                filter: "\"AsaasPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BillingPayments_CompanyId_DueDate",
                table: "BillingPayments",
                columns: new[] { "CompanyId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingPayments_CompanyId_Kind_PeriodStart",
                table: "BillingPayments",
                columns: new[] { "CompanyId", "Kind", "PeriodStart" },
                unique: true,
                filter: "\"PeriodStart\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BillingPayments_SubscriptionId",
                table: "BillingPayments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_BillingWebhookEvents_EventId",
                table: "BillingWebhookEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingWebhookEvents_ProcessedAt",
                table: "BillingWebhookEvents",
                column: "ProcessedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Plans_PendingPlanId",
                table: "Subscriptions",
                column: "PendingPlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Asaas only charges in reais: every plan is priced in BRL from now on.
            migrationBuilder.Sql(@"UPDATE ""Plans"" SET ""Currency"" = 'BRL';");
            // Admin-assigned plans are billed outside the app.
            migrationBuilder.Sql(@"UPDATE ""Subscriptions"" SET ""PaymentMethod"" = 'Manual' WHERE ""AssignedByAdminId"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Plans_PendingPlanId",
                table: "Subscriptions");

            migrationBuilder.DropTable(
                name: "BillingCheckouts");

            migrationBuilder.DropTable(
                name: "BillingPayments");

            migrationBuilder.DropTable(
                name: "BillingWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_AsaasSubscriptionId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_CompanyId_Status",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_PendingPlanId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "AsaasSubscriptionId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CancelAtPeriodEnd",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CardBrand",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CardLast4",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CardToken",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Cycle",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PastDueSince",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PendingPlanId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "YearlyPrice",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "AsaasCustomerId",
                table: "Companies");

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Plans",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD",
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldDefaultValue: "BRL");

            migrationBuilder.AddColumn<string>(
                name: "StripePriceId",
                table: "Plans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_CompanyId",
                table: "Subscriptions",
                column: "CompanyId");
        }
    }
}
