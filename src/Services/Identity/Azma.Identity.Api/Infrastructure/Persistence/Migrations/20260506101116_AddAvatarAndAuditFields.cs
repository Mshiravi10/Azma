using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Azma.Identity.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarAndAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarObjectKey",
                schema: "identity",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "identity",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now() at time zone 'utc'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarObjectKey",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "identity",
                table: "users");
        }
    }
}
