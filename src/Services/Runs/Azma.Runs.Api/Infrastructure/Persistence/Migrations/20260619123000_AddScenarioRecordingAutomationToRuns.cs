using Azma.Runs.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Runs.Api.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(RunsDbContext))]
    [Migration("20260619123000_AddScenarioRecordingAutomationToRuns")]
    public partial class AddScenarioRecordingAutomationToRuns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE runs."testcase_refs"
                    ADD COLUMN IF NOT EXISTS "AutomationEngine" integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS "AutomationStatus" integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS "AutomationDefinitionVersion" integer NULL,
                    ADD COLUMN IF NOT EXISTS "AutomationRecordingJson" jsonb NULL;

                ALTER TABLE runs."run_cases"
                    ADD COLUMN IF NOT EXISTS "AutomationEngine" integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS "AutomationStatus" integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS "AutomationDefinitionVersion" integer NULL,
                    ADD COLUMN IF NOT EXISTS "AutomationRecordingJson" jsonb NULL;

                ALTER TABLE runs."run_results"
                    ADD COLUMN IF NOT EXISTS "AutomationEngine" integer NULL,
                    ADD COLUMN IF NOT EXISTS "AutomationDetailJson" jsonb NULL;

                ALTER TABLE runs."run_schedule_cases"
                    ALTER COLUMN "AutomationScenarioPath" DROP NOT NULL,
                    ADD COLUMN IF NOT EXISTS "AutomationEngine" integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS "AutomationStatus" integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS "AutomationDefinitionVersion" integer NULL,
                    ADD COLUMN IF NOT EXISTS "AutomationRecordingJson" jsonb NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE runs."run_schedule_cases"
                    DROP COLUMN IF EXISTS "AutomationRecordingJson",
                    DROP COLUMN IF EXISTS "AutomationDefinitionVersion",
                    DROP COLUMN IF EXISTS "AutomationStatus",
                    DROP COLUMN IF EXISTS "AutomationEngine";

                ALTER TABLE runs."run_results"
                    DROP COLUMN IF EXISTS "AutomationDetailJson",
                    DROP COLUMN IF EXISTS "AutomationEngine";

                ALTER TABLE runs."run_cases"
                    DROP COLUMN IF EXISTS "AutomationRecordingJson",
                    DROP COLUMN IF EXISTS "AutomationDefinitionVersion",
                    DROP COLUMN IF EXISTS "AutomationStatus",
                    DROP COLUMN IF EXISTS "AutomationEngine";

                ALTER TABLE runs."testcase_refs"
                    DROP COLUMN IF EXISTS "AutomationRecordingJson",
                    DROP COLUMN IF EXISTS "AutomationDefinitionVersion",
                    DROP COLUMN IF EXISTS "AutomationStatus",
                    DROP COLUMN IF EXISTS "AutomationEngine";
                """);
        }
    }
}
