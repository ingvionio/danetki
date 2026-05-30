using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Danetka.AuthService.Migrations
{
    /// <inheritdoc />
    public partial class AddSaaSFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "subscription_plan",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Trial");

            migrationBuilder.AddColumn<int>(
                name: "tokens",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "subscription_plan",
                table: "users");

            migrationBuilder.DropColumn(
                name: "tokens",
                table: "users");
        }
    }
}
