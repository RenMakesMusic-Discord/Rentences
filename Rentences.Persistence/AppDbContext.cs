using Microsoft.EntityFrameworkCore;
using Rentences.Domain.Definitions.Game;

namespace Rentences.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DbSets for each entity
        public DbSet<Word> Words { get; set; }
        public DbSet<WordUsage> WordUsages { get; set; }
        public DbSet<UserWordStatistics> UserStatistics { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            
            modelBuilder.Entity<Word>()
                .HasKey(w => w.MessageId);

            // Configure unique index on WordValue in WordUsage to prevent duplicates
            modelBuilder.Entity<WordUsage>()
                .HasIndex(w => w.WordValue)
                .IsUnique();

            // Configure unique index on UserId in UserWordStatistics
            modelBuilder.Entity<UserWordStatistics>()
                .HasIndex(u => u.UserId)
                .IsUnique();
        }
    }
}
