using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtTriggers : Migration
    {
        private static readonly string[] TablesWithUpdatedAt =
        [
            "Users", "Companies", "Agents", "AgentFiles",
            "Plans", "Subscriptions", "Invitations", "AIModels"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create shared trigger function
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION update_updated_at_column()
                RETURNS TRIGGER AS $$
                BEGIN
                    NEW."UpdatedAt" = NOW() AT TIME ZONE 'UTC';
                    RETURN NEW;
                END;
                $$ language 'plpgsql';
                """);

            // Create trigger for each entity table
            foreach (var table in TablesWithUpdatedAt)
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER trigger_update_{table.ToLower()}_updated_at
                    BEFORE UPDATE ON "{table}"
                    FOR EACH ROW
                    EXECUTE PROCEDURE update_updated_at_column();
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TablesWithUpdatedAt)
            {
                migrationBuilder.Sql($"""
                    DROP TRIGGER IF EXISTS trigger_update_{table.ToLower()}_updated_at ON "{table}";
                    """);
            }

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_updated_at_column();");
        }
    }
}
