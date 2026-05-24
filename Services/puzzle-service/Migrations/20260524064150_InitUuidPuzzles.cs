using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PuzzleService.Migrations
{
    /// <inheritdoc />
    public partial class InitUuidPuzzles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "puzzles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OpenPart = table.Column<string>(type: "text", nullable: false),
                    HiddenPart = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    StoryId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_puzzles", x => x.Id);
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
