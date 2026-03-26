using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRateLimitsToPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            // Seed initial limits per plan tier:
            //   Starter     (Id=1): 500 conversations / 5000 messages
            //   Professional(Id=2): 2000 conversations / 20000 messages
            //   Scale       (Id=3): unlimited (0)
            migrationBuilder.Sql("""
                UPDATE "Plans" SET "MaxConversationsPerMonth" = 500,  "MaxMessagesPerMonth" = 5000  WHERE "Id" = 1;
                UPDATE "Plans" SET "MaxConversationsPerMonth" = 2000, "MaxMessagesPerMonth" = 20000 WHERE "Id" = 2;
                UPDATE "Plans" SET "MaxConversationsPerMonth" = 0,    "MaxMessagesPerMonth" = 0     WHERE "Id" = 3;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MaxConversationsPerMonth", table: "Plans");
            migrationBuilder.DropColumn(name: "MaxMessagesPerMonth", table: "Plans");
        }
    }
}
