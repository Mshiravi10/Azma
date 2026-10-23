using System;
using Azma.TestCases.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.TestCases.Api.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(TestCasesDbContext))]
    [Migration("20260614120000_AddLastExecutionMetadata")]
    public partial class AddLastExecutionMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastExecutedAtUtc",
                schema: "testcases",
                table: "testcases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastExecutionStatus",
                schema: "testcases",
                table: "testcases",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastExecutionRunId",
                schema: "testcases",
                table: "testcases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastExecutionRunCaseId",
                schema: "testcases",
                table: "testcases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastExecutedByUserId",
                schema: "testcases",
                table: "testcases",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE testcases."testcases" tc
                SET
                    "LastExecutedAtUtc" = latest."ExecutedAtUtc",
                    "LastExecutionStatus" = latest."StatusText",
                    "LastExecutionRunId" = latest."RunId",
                    "LastExecutionRunCaseId" = latest."RunCaseId",
                    "LastExecutedByUserId" = latest."ExecutedByUserId"
                FROM (
                    SELECT DISTINCT ON (rc."TestCaseId")
                        rc."TestCaseId",
                        r."Id" AS "RunId",
                        rc."Id" AS "RunCaseId",
                        rr."ExecutedAtUtc",
                        rr."ExecutedByUserId",
                        CASE rr."Status"
                            WHEN 1 THEN 'Passed'
                            WHEN 2 THEN 'Failed'
                            WHEN 3 THEN 'Blocked'
                            WHEN 4 THEN 'Retest'
                            WHEN 5 THEN 'Skipped'
                            ELSE 'Untested'
                        END AS "StatusText"
                    FROM runs."run_results" rr
                    INNER JOIN runs."run_cases" rc ON rc."Id" = rr."RunCaseId"
                    INNER JOIN runs."runs" r ON r."Id" = rc."RunId"
                    ORDER BY rc."TestCaseId", rr."ExecutedAtUtc" DESC
                ) latest
                WHERE tc."Id" = latest."TestCaseId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_testcases_WorkspaceId_ProjectId_LastExecutedAtUtc",
                schema: "testcases",
                table: "testcases",
                columns: new[] { "WorkspaceId", "ProjectId", "LastExecutedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_testcases_WorkspaceId_ProjectId_LastExecutedAtUtc",
                schema: "testcases",
                table: "testcases");

            migrationBuilder.DropColumn(
                name: "LastExecutedAtUtc",
                schema: "testcases",
                table: "testcases");

            migrationBuilder.DropColumn(
                name: "LastExecutionStatus",
                schema: "testcases",
                table: "testcases");

            migrationBuilder.DropColumn(
                name: "LastExecutionRunId",
                schema: "testcases",
                table: "testcases");

            migrationBuilder.DropColumn(
                name: "LastExecutionRunCaseId",
                schema: "testcases",
                table: "testcases");

            migrationBuilder.DropColumn(
                name: "LastExecutedByUserId",
                schema: "testcases",
                table: "testcases");
        }
    }
}
