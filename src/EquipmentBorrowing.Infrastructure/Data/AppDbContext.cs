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

            entity.Property(s => s.StudentNumber).IsRequired().HasMaxLength(50);
            entity.Property(s => s.StudentNumberKey).IsRequired().HasMaxLength(50);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
            entity.Property(s => s.Contact).HasMaxLength(200);
            entity.Property(s => s.IsAllowedToBorrow).IsRequired();

            // BR-01 / BR-16: unique student number, ignoring case and surrounding spaces
            entity.HasIndex(s => s.StudentNumberKey)
                .IsUnique()
                .HasDatabaseName("UX_Students_StudentNumberKey");
        });

        // ---------- Equipment ----------
        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Identifier).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IdentifierKey).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsAvailable).IsRequired();

            // BR-05 / BR-16: unique equipment identifier, ignoring case and surrounding spaces
            entity.HasIndex(e => e.IdentifierKey)
                .IsUnique()
                .HasDatabaseName("UX_Equipment_IdentifierKey");
        });

        // ---------- Borrowing ----------
        modelBuilder.Entity<Borrowing>(entity =>
        {
            entity.HasKey(b => b.Id);

            // Only the calendar date is stored (PostgreSQL "date")
            entity.Property(b => b.DateBorrowed).IsRequired().HasColumnType("date");
            entity.Property(b => b.ExpectedReturnDate).IsRequired().HasColumnType("date");
            entity.Property(b => b.ReturnedDate).HasColumnType("date");

            entity.Property(b => b.Status).IsRequired().HasConversion<int>();

            entity.HasOne<Student>()
                .WithMany()
                .HasForeignKey(b => b.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Equipment>()
                .WithMany()
                .HasForeignKey(b => b.EquipmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // BR-10 / BR-16: an item can have at most ONE active borrowing.
            // Partial unique index: only rows with Status = 0 (Active) are checked.
            entity.HasIndex(b => b.EquipmentId)
                .IsUnique()
                .HasFilter("\"Status\" = 0")
                .HasDatabaseName("UX_Borrowings_OneActivePerEquipment");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Borrowings_ExpectedAfterCheckout",
                    "\"ExpectedReturnDate\" > \"DateBorrowed\"");
                t.HasCheckConstraint("CK_Borrowings_ReturnNotBeforeCheckout",
                    "\"ReturnedDate\" IS NULL OR \"ReturnedDate\" >= \"DateBorrowed\"");
            });
        });
    }
}