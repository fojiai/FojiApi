using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FojiApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableCrmOnAllPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The HasCrm flag was added (migration AddCrmEntitiesAndPlanGate) with a
            // default of false and never backfilled, so every existing plan — including
            // the top tier — had the CRM module gated off. Product decision: CRM is
            // available on all plans, so enable it for every existing plan.
            migrationBuilder.Sql(@"UPDATE ""Plans"" SET ""HasCrm"" = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Plans"" SET ""HasCrm"" = false;");
        }
    }
}
