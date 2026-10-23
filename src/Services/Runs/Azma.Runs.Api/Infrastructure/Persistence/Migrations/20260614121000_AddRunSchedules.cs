using System;
using Azma.Runs.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Runs.Api.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(RunsDbContext))]
    [Migration("20260614121000_AddRunSchedules")]
    public partial class AddRunSchedules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "run_schedules",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Environment = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RecurrenceType = table.Column<int>(type: "integer", nullable: false),
                    TimeOfDayMinutes = table.Column<int>(type: "integer", nullable: false),
                    WeekDaysMask = table.Column<int>(type: "integer", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NextRunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Loops = table.Column<int>(type: "integer", nullable: false),
                    Threads = table.Column<int>(type: "integer", nullable: false),
                    Rampup = table.Column<int>(type: "integer", nullable: false),
                    Skippable = table.Column<bool>(type: "boolean", nullable: false),
                    Gateway = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Frontend = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_schedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "run_schedule_cases",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCaseCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TestCaseTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TestCaseVersion = table.Column<int>(type: "integer", nullable: false),
                    AutomationScenarioPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_schedule_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_schedule_cases_run_schedules_RunScheduleId",
                        column: x => x.RunScheduleId,
                        principalSchema: "runs",
                        principalTable: "run_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_schedule_executions",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalCases = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<int>(type: "integer", nullable: false),
                    Failed = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_schedule_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_schedule_executions_run_schedules_RunScheduleId",
                        column: x => x.RunScheduleId,
                        principalSchema: "runs",
                        principalTable: "run_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_run_schedules_IsActive_NextRunAtUtc_LockedUntilUtc",
                schema: "runs",
                table: "run_schedules",
                columns: new[] { "IsActive", "NextRunAtUtc", "LockedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_run_schedules_WorkspaceId_ProjectId_IsActive_DeletedAtUtc",
                schema: "runs",
                table: "run_schedules",
                columns: new[] { "WorkspaceId", "ProjectId", "IsActive", "DeletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_run_schedule_cases_RunScheduleId_TestCaseId",
                schema: "runs",
                table: "run_schedule_cases",
                columns: new[] { "RunScheduleId", "TestCaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_run_schedule_executions_RunId",
                schema: "runs",
                table: "run_schedule_executions",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_run_schedule_executions_RunScheduleId_StartedAtUtc",
                schema: "runs",
                table: "run_schedule_executions",
                columns: new[] { "RunScheduleId", "StartedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "run_schedule_cases",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_schedule_executions",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_schedules",
                schema: "runs");
        }
    }
}
