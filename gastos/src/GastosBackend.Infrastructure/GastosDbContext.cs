using GastosBackend.Domain.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GastosBackend.Infrastructure
{
    public class GastosDbContext : DbContext, IDataProtectionKeyContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<UserToken> UserTokens { get; set; }
        public DbSet<BudgetAlert> BudgetAlerts { get; set; }
        public DbSet<RecoveryCode> RecoveryCodes { get; set; }

        /// <summary>The keys that encrypt the 2FA secrets. They live in the database: a container loses its disk on every restart.</summary>
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

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
                entity.Property(c => c.MonthlyBudget).HasPrecision(18, 2);

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
                entity.Property(t => t.BlueExchangeRate).HasPrecision(18, 4);
                entity.Property(t => t.MepExchangeRate).HasPrecision(18, 4);
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

            modelBuilder.Entity<UserToken>(entity =>
            {
                entity.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(t => t.TokenHash);
            });

            modelBuilder.Entity<RecoveryCode>(entity =>
            {
                entity.Property(r => r.CodeHash).HasMaxLength(100).IsRequired();

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(r => r.UserId);
            });

            modelBuilder.Entity<BudgetAlert>(entity =>
            {
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Category>()
                    .WithMany()
                    .HasForeignKey(a => a.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                // One mark per user, category, month and level. Soft-deleted marks (a mail that failed) do not count,
                // so the alert can be sent again.
                entity.HasIndex(a => new { a.UserId, a.CategoryId, a.Year, a.Month, a.Level })
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");
            });
        }
    }
}
