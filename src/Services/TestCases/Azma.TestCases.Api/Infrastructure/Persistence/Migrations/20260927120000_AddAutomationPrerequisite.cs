using System;
using Azma.TestCases.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.TestCases.Api.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(TestCasesDbContext))]
    [Migration("20260927120000_AddAutomationPrerequisite")]
    public partial class AddAutomationPrerequisite : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AutomationPrerequisiteTestCaseId",
                schema: "testcases",
                table: "testcases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_testcases_AutomationPrerequisiteTestCaseId",
                schema: "testcases",
                table: "testcases",
                column: "AutomationPrerequisiteTestCaseId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_testcases_AutomationPrerequisiteTestCaseId",
                schema: "testcases",
                table: "testcases");

            migrationBuilder.DropColumn(
                name: "AutomationPrerequisiteTestCaseId",
                schema: "testcases",
                table: "testcases");
        }
    }
}
