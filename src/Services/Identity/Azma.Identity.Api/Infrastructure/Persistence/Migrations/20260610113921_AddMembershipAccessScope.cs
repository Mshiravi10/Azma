using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Identity.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipAccessScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessScope",
                schema: "identity",
                table: "memberships",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessScope",
                schema: "identity",
                table: "memberships");
        }
    }
}
