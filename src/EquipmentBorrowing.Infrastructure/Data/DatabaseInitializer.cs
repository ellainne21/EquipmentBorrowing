using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Data;

public static class DatabaseInitializer
{
    // Applies pending migrations, then adds starting data ONLY if the database is empty.
    // It never deletes or overwrites existing data.
    public static void Initialize(AppDbContext context)
    {
        context.Database.Migrate();
        SeedIfEmpty(context);
    }

    private static void SeedIfEmpty(AppDbContext context)
    {
        if (context.Students.Any() || context.Equipment.Any() || context.Borrowings.Any())
            return;

        using var transaction = context.Database.BeginTransaction();

        var alice = new Student("2026-0001", "Alice Santos", "alice.santos@example.com");
        var bob = new Student("2026-0002", "Bob Reyes", "0917 000 0002");
        bob.SetEligibility(false); // an ineligible student
        var carla = new Student("2026-0003", "Carla Mendoza");      // no history
        var daniel = new Student("2026-0004", "Daniel Cruz");       // no history
        context.Students.AddRange(alice, bob, carla, daniel);

        var laptop = new Equipment("LAP-001", "Laptop Dell XPS 15", "15-inch laptop with charger");
        var projector = new Equipment("PRJ-001", "Projector Epson", "HDMI projector");
        var arduino = new Equipment("ARD-001", "Arduino Kit", "Starter kit");
        var camera = new Equipment("CAM-001", "Canon Camera", "DSLR with 18-55mm lens");
        var microphone = new Equipment("MIC-001", "USB Microphone"); // no history
        context.Equipment.AddRange(laptop, projector, arduino, camera, microphone);

        context.SaveChanges(); // IDs are generated here

        var today = DateTime.Today;

        // Returned history
        var returnedByAlice = new Borrowing(alice.Id, laptop.Id, today.AddDays(-14), today.AddDays(-7));
        returnedByAlice.MarkAsReturned(today.AddDays(-8));
        var returnedByBob = new Borrowing(bob.Id, arduino.Id, today.AddDays(-20), today.AddDays(-13));
        returnedByBob.MarkAsReturned(today.AddDays(-15));

        // One active borrowing (the camera is therefore unavailable)
        var activeByAlice = new Borrowing(alice.Id, camera.Id, today.AddDays(-1), today.AddDays(6));
        camera.MarkAsBorrowed();

        context.Borrowings.AddRange(returnedByAlice, returnedByBob, activeByAlice);
        context.SaveChanges();

        transaction.Commit();
    }
}