using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.TestCases.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationScenarioPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutomationScenarioPath",
                schema: "testcases",
                table: "testcases",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutomationScenarioPath",
                schema: "testcases",
                table: "testcases");
        }
    }
}
