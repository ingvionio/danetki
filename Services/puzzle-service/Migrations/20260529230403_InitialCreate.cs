using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PuzzleService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "puzzles",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    open_part = table.Column<string>(type: "text", nullable: false),
                    hidden_part = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    story_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_puzzles", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "puzzles");
        }
    }
}
