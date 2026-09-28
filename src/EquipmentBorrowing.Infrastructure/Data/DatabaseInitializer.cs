using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Data;

public static class DatabaseInitializer
{
    // Applies any migrations that have not been applied yet.
    // It does NOT recreate the database. Seed data will be added here in Part L.
    public static void Initialize(AppDbContext context)
    {
        context.Database.Migrate();
    }
}