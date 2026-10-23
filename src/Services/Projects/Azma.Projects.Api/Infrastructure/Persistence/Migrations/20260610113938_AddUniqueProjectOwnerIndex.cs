using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Projects.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueProjectOwnerIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_project_memberships_ProjectId",
                schema: "projects",
                table: "project_memberships",
                column: "ProjectId",
                unique: true,
                filter: "\"Role\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_project_memberships_ProjectId",
                schema: "projects",
                table: "project_memberships");
        }
    }
}
