using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    StatDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sessions = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Messages = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
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
                name: "IX_DailyStats_CompanyId_StatDate",
                table: "DailyStats",
                columns: new[] { "CompanyId", "StatDate" },
                unique: true);

            // UpdatedAt trigger for DailyStats
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION update_daily_stats_updated_at()
                RETURNS TRIGGER AS $$
                BEGIN
                    NEW."UpdatedAt" = NOW() AT TIME ZONE 'UTC';
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_daily_stats_updated_at
                BEFORE UPDATE ON "DailyStats"
                FOR EACH ROW EXECUTE FUNCTION update_daily_stats_updated_at();
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_daily_stats_updated_at ON ""DailyStats"";");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS update_daily_stats_updated_at();");
            migrationBuilder.DropTable(name: "DailyStats");
        }
    }
}
