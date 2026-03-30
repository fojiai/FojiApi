using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MonthlyPriceUsd",
                table: "Plans",
                newName: "MonthlyPrice");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Plans",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Plans");

            migrationBuilder.RenameColumn(
                name: "MonthlyPrice",
                table: "Plans",
                newName: "MonthlyPriceUsd");
        }
    }
}
