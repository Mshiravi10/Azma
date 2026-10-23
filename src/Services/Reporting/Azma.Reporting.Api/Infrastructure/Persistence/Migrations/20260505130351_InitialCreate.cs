using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Reporting.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "project_refs",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_refs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "result_events",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExecutedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_result_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "runs",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CaseCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<int>(type: "integer", nullable: false),
                    Failed = table.Column<int>(type: "integer", nullable: false),
                    Blocked = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    Retest = table.Column<int>(type: "integer", nullable: false),
                    Recorded = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "testcase_refs",
                schema: "reporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_testcase_refs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_refs_WorkspaceId",
                schema: "reporting",
                table: "project_refs",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_result_events_RunId",
                schema: "reporting",
                table: "result_events",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_result_events_WorkspaceId_ExecutedByUserId_ExecutedAtUtc",
                schema: "reporting",
                table: "result_events",
                columns: new[] { "WorkspaceId", "ExecutedByUserId", "ExecutedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_result_events_WorkspaceId_ProjectId_ExecutedAtUtc",
                schema: "reporting",
                table: "result_events",
                columns: new[] { "WorkspaceId", "ProjectId", "ExecutedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_result_events_WorkspaceId_TestCaseId_Status",
                schema: "reporting",
                table: "result_events",
                columns: new[] { "WorkspaceId", "TestCaseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_runs_WorkspaceId_ProjectId_CreatedAtUtc",
                schema: "reporting",
                table: "runs",
                columns: new[] { "WorkspaceId", "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_runs_WorkspaceId_Status",
                schema: "reporting",
                table: "runs",
                columns: new[] { "WorkspaceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_testcase_refs_WorkspaceId_ProjectId",
                schema: "reporting",
                table: "testcase_refs",
                columns: new[] { "WorkspaceId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_refs",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "result_events",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "runs",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "testcase_refs",
                schema: "reporting");
        }
    }
}
