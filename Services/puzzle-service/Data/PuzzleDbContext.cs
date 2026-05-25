using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PuzzleService.Models;

namespace PuzzleService.Data
{
    public class PuzzleDbContext : DbContext
    {
        public PuzzleDbContext(DbContextOptions<PuzzleDbContext> options) : base(options)
        {
        }

        public DbSet<PuzzleEntity> Puzzles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var puzzle = modelBuilder.Entity<PuzzleEntity>();

            puzzle.ToTable("puzzles");
            puzzle.HasKey(p => p.Id);

            // Схема таблицы создаётся init.sql со snake_case колонками,
            // поэтому маппим явно — иначе EF использует имена свойств.
            puzzle.Property(p => p.Id).HasColumnName("id");
            puzzle.Property(p => p.OpenPart).HasColumnName("open_part");
            puzzle.Property(p => p.HiddenPart).HasColumnName("hidden_part");
            puzzle.Property(p => p.SourceUrl).HasColumnName("source_url");
            puzzle.Property(p => p.StoryId).HasColumnName("story_id");
            puzzle.Property(p => p.CreatedAt).HasColumnName("created_at");

            // В init.sql id это UUID, а в модели string — конвертим вручную.
            var stringToGuid = new ValueConverter<string, System.Guid>(
                s => System.Guid.Parse(s),
                g => g.ToString());

            puzzle.Property(p => p.Id)
                .HasColumnType("uuid")
                .HasConversion(stringToGuid);
        }
    }
}
