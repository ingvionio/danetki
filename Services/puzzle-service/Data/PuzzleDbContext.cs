using Microsoft.EntityFrameworkCore;
using PuzzleService.Models;

namespace PuzzleService.Data
{
    public class PuzzleDbContext : DbContext
    {
        public PuzzleDbContext(DbContextOptions<PuzzleDbContext> options) : base(options)
        {
        }
        
        // Это наша таблица данеток в базе данных
        public DbSet<PuzzleEntity> Puzzles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Явно указываем имя таблицы в PostgreSQL
            modelBuilder.Entity<PuzzleEntity>().ToTable("puzzles");
            
            // Настраиваем первичный ключ
            modelBuilder.Entity<PuzzleEntity>().HasKey(p => p.Id);
        }
    }
}