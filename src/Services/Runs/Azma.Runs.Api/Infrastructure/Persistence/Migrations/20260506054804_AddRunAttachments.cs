using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Runs.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "run_attachments",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_run_attachments_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "runs",
                        principalTable: "runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_run_attachments_RunCaseId",
                schema: "runs",
                table: "run_attachments",
                column: "RunCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_run_attachments_RunId",
                schema: "runs",
                table: "run_attachments",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_run_attachments_RunResultId",
                schema: "runs",
                table: "run_attachments",
                column: "RunResultId");

            migrationBuilder.CreateIndex(
                name: "IX_run_attachments_WorkspaceId_RunId_UploadedAtUtc",
                schema: "runs",
                table: "run_attachments",
                columns: new[] { "WorkspaceId", "RunId", "UploadedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "run_attachments",
                schema: "runs");
        }
    }
}
