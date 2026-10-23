using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Runs.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "runs");

            migrationBuilder.CreateTable(
                name: "project_refs",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    NextRunSequence = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_refs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "runs",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "testcase_refs",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_testcase_refs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "run_cases",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCaseCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TestCaseTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TestCaseVersion = table.Column<int>(type: "integer", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentStatus = table.Column<int>(type: "integer", nullable: false),
                    LastExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastExecutedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_cases_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "runs",
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_comments",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_comments_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "runs",
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_results",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ElapsedSeconds = table.Column<int>(type: "integer", nullable: true),
                    ExecutedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_results_run_cases_RunCaseId",
                        column: x => x.RunCaseId,
                        principalSchema: "runs",
                        principalTable: "run_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_refs_WorkspaceId",
                schema: "runs",
                table: "project_refs",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_run_cases_RunId_CurrentStatus",
                schema: "runs",
                table: "run_cases",
                columns: new[] { "RunId", "CurrentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_run_cases_RunId_TestCaseId",
                schema: "runs",
                table: "run_cases",
                columns: new[] { "RunId", "TestCaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_run_comments_RunCaseId",
                schema: "runs",
                table: "run_comments",
                column: "RunCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_run_comments_RunId_CreatedAtUtc",
                schema: "runs",
                table: "run_comments",
                columns: new[] { "RunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_run_results_RunCaseId_ExecutedAtUtc",
                schema: "runs",
                table: "run_results",
                columns: new[] { "RunCaseId", "ExecutedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_runs_WorkspaceId_Code",
                schema: "runs",
                table: "runs",
                columns: new[] { "WorkspaceId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_runs_WorkspaceId_ProjectId_Status_DeletedAtUtc",
                schema: "runs",
                table: "runs",
                columns: new[] { "WorkspaceId", "ProjectId", "Status", "DeletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_testcase_refs_WorkspaceId_ProjectId",
                schema: "runs",
                table: "testcase_refs",
                columns: new[] { "WorkspaceId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_refs",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_comments",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_results",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "testcase_refs",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "run_cases",
                schema: "runs");

            migrationBuilder.DropTable(
                name: "runs",
                schema: "runs");
        }
    }
}
