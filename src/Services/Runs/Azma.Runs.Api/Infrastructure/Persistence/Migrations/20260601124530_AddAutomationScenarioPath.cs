using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Runs.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationScenarioPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE runs.testcase_refs
                ADD COLUMN IF NOT EXISTS "AutomationScenarioPath" character varying(1000);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE runs.runs
                ADD COLUMN IF NOT EXISTS "DueDateUtc" timestamp with time zone;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE runs.runs
                ADD COLUMN IF NOT EXISTS "Environment" text;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE runs.run_cases
                ADD COLUMN IF NOT EXISTS "AutomationScenarioPath" character varying(1000);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE runs.testcase_refs
                DROP COLUMN IF EXISTS "AutomationScenarioPath";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE runs.runs
                DROP COLUMN IF EXISTS "DueDateUtc";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE runs.runs
                DROP COLUMN IF EXISTS "Environment";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE runs.run_cases
                DROP COLUMN IF EXISTS "AutomationScenarioPath";
                """);
        }
    }
}
