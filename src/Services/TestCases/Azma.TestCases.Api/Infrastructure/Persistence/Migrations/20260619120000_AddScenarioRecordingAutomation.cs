using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.TestCases.Api.Infrastructure.Persistence.Migrations;

public partial class AddScenarioRecordingAutomation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AutomationEngine",
            schema: "testcases",
            table: "testcases",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "AutomationStatus",
            schema: "testcases",
            table: "testcases",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "AutomationDefinitionVersion",
            schema: "testcases",
            table: "testcases",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AutomationRecordingJson",
            schema: "testcases",
            table: "testcases",
            type: "jsonb",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "automation_recording_sessions",
            schema: "testcases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                EventsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_automation_recording_sessions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_automation_recording_sessions_SessionToken",
            schema: "testcases",
            table: "automation_recording_sessions",
            column: "SessionToken",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_automation_recording_sessions_WorkspaceId_TestCaseId_StartedAtUtc",
            schema: "testcases",
            table: "automation_recording_sessions",
            columns: new[] { "WorkspaceId", "TestCaseId", "StartedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "automation_recording_sessions",
            schema: "testcases");

        migrationBuilder.DropColumn(
            name: "AutomationEngine",
            schema: "testcases",
            table: "testcases");

        migrationBuilder.DropColumn(
            name: "AutomationStatus",
            schema: "testcases",
            table: "testcases");

        migrationBuilder.DropColumn(
            name: "AutomationDefinitionVersion",
            schema: "testcases",
            table: "testcases");

        migrationBuilder.DropColumn(
            name: "AutomationRecordingJson",
            schema: "testcases",
            table: "testcases");
    }
}
