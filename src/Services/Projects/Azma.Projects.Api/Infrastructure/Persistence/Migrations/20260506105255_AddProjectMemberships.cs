using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Projects.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_memberships",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_memberships_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_memberships_ProjectId_UserId",
                schema: "projects",
                table: "project_memberships",
                columns: new[] { "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_memberships_UserId",
                schema: "projects",
                table: "project_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_project_memberships_WorkspaceId",
                schema: "projects",
                table: "project_memberships",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_memberships",
                schema: "projects");
        }
    }
}
