using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.TestCases.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "testcases");

            migrationBuilder.CreateTable(
                name: "project_refs",
                schema: "testcases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    NextSequence = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_refs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "testcases",
                schema: "testcases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Preconditions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_testcases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "test_steps",
                schema: "testcases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExpectedResult = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_test_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_test_steps_testcases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testcases",
                        principalTable: "testcases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "testcase_tags",
                schema: "testcases",
                columns: table => new
                {
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tag = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_testcase_tags", x => new { x.TestCaseId, x.Tag });
                    table.ForeignKey(
                        name: "FK_testcase_tags_testcases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testcases",
                        principalTable: "testcases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "testcase_versions",
                schema: "testcases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    ChangeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_testcase_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_testcase_versions_testcases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testcases",
                        principalTable: "testcases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_refs_WorkspaceId",
                schema: "testcases",
                table: "project_refs",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_test_steps_TestCaseId_Order",
                schema: "testcases",
                table: "test_steps",
                columns: new[] { "TestCaseId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_testcase_versions_TestCaseId_Version",
                schema: "testcases",
                table: "testcase_versions",
                columns: new[] { "TestCaseId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_testcases_ProjectId_SuiteId_SectionId_DeletedAtUtc",
                schema: "testcases",
                table: "testcases",
                columns: new[] { "ProjectId", "SuiteId", "SectionId", "DeletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_testcases_WorkspaceId_Code",
                schema: "testcases",
                table: "testcases",
                columns: new[] { "WorkspaceId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_testcases_WorkspaceId_DeletedAtUtc",
                schema: "testcases",
                table: "testcases",
                columns: new[] { "WorkspaceId", "DeletedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_refs",
                schema: "testcases");

            migrationBuilder.DropTable(
                name: "test_steps",
                schema: "testcases");

            migrationBuilder.DropTable(
                name: "testcase_tags",
                schema: "testcases");

            migrationBuilder.DropTable(
                name: "testcase_versions",
                schema: "testcases");

            migrationBuilder.DropTable(
                name: "testcases",
                schema: "testcases");
        }
    }
}
