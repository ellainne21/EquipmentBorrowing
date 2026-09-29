using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Data;

public static class DatabaseInitializer
{
    // Applies any migrations that have not been applied yet, then seeds
    // starting data only if the tables are empty (so restarting the app
    // never duplicates rows).
    public static void Initialize(AppDbContext context)
    {
        context.Database.Migrate();
        SeedIfEmpty(context);
    }

    private static void SeedIfEmpty(AppDbContext context)
    {
        if (!context.Students.Any())
        {
            context.Students.Add(new Student(1, "Alice Santos", true));
            context.Students.Add(new Student(2, "Bob Reyes", false));
        }

        if (!context.Equipment.Any())
        {
            context.Equipment.Add(new Equipment(101, "Laptop Dell XPS 15"));
            context.Equipment.Add(new Equipment(102, "Projector Epson"));
            context.Equipment.Add(new Equipment(103, "Arduino Kit"));

            // One item that is already unavailable, so the "equipment
            // unavailable" failure case can be demonstrated in the UI.
            var camera = new Equipment(104, "Canon Camera");
            camera.MarkAsBorrowed();
            context.Equipment.Add(camera);
        }

        context.SaveChanges();
    }
}