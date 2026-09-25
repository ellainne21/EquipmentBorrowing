using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<Borrowing> Borrowings => Set<Borrowing>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Student ----------
        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(s => s.IsAllowedToBorrow)
                .IsRequired();
        });

        // ---------- Equipment ----------
        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.IsAvailable)
                .IsRequired();
        });

        // ---------- Borrowing ----------
        modelBuilder.Entity<Borrowing>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.DateBorrowed)
                .IsRequired();

            entity.Property(b => b.ExpectedReturnDate)
                .IsRequired();

            // Store the enum as its underlying int value in SQLite
            entity.Property(b => b.Status)
                .IsRequired()
                .HasConversion<int>();

            // Foreign key relationships
            entity.HasOne<Student>()
                .WithMany()
                .HasForeignKey(b => b.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(b => b.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}