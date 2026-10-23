using System;
using Azma.Runs.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Runs.Api.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(RunsDbContext))]
    [Migration("20260615140000_AddRunAutomationExecutions")]
    public partial class AddRunAutomationExecutions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "run_automation_executions",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_run_automation_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_automation_executions_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "runs",
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_run_automation_executions_RunId_StartedAtUtc",
                schema: "runs",
                table: "run_automation_executions",
                columns: new[] { "RunId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_run_automation_executions_Status_StartedAtUtc",
                schema: "runs",
                table: "run_automation_executions",
                columns: new[] { "Status", "StartedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "run_automation_executions",
                schema: "runs");
        }
    }
}
