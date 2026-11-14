using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EquipmentBorrowing.Infrastructure.Data;

// Used only by the dotnet ef tools (for example when creating a migration).
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(DatabaseSettings.GetConnectionString("Main"))
            .Options;

        return new AppDbContext(options);
    }
}