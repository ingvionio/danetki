using Microsoft.EntityFrameworkCore;
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
            modelBuilder.Entity<PuzzleEntity>(entity =>
            {
                entity.ToTable("puzzles");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.OpenPart).HasColumnName("open_part").IsRequired();
                entity.Property(e => e.HiddenPart).HasColumnName("hidden_part").IsRequired();
                entity.Property(e => e.SourceUrl).HasColumnName("source_url").IsRequired();
                entity.Property(e => e.StoryId).HasColumnName("story_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            });
        }
    }
}
