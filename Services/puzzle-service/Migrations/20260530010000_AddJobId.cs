using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PuzzleService.Migrations
{
    /// <inheritdoc />
    public partial class AddJobId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "job_id",
                table: "puzzles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_puzzles_job_id",
                table: "puzzles",
                column: "job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_puzzles_job_id",
                table: "puzzles");

            migrationBuilder.DropColumn(
                name: "job_id",
                table: "puzzles");
        }
    }
}
