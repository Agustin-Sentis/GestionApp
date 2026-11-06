using GastosBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.Infrastructure
{
    public class GastosDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        public GastosDbContext(DbContextOptions<GastosDbContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.Email).HasMaxLength(254).IsRequired();
                entity.Property(u => u.Password).IsRequired();

                // Único solo entre usuarios no borrados (soft delete)
                entity.HasIndex(u => u.Email).IsUnique().HasFilter("\"IsDeleted\" = false");
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.Property(c => c.Name).HasMaxLength(60).IsRequired();

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // The unique index on (UserId, lower("Name")) WHERE "IsDeleted" = false is created with raw SQL in the
                // migration "CaseInsensitiveCategoryNames": EF Core cannot model an index on an expression.
                // It ignores soft-deleted rows so a deleted category does not block creating another one with the same name.
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.Property(t => t.Amount).HasPrecision(18, 2);
                entity.Property(t => t.ExchangeRate).HasPrecision(18, 4);
                entity.Property(t => t.Note).HasMaxLength(200);

                entity.HasOne(t => t.Category)
                    .WithMany()
                    .HasForeignKey(t => t.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => new { t.UserId, t.Date });
            });
        }
    }
}
