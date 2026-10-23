using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Runs.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users_read_model",
                schema: "runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AvatarObjectKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users_read_model", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_read_model_Email",
                schema: "runs",
                table: "users_read_model",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users_read_model",
                schema: "runs");
        }
    }
}
