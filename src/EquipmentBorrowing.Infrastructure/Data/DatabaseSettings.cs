using Microsoft.Extensions.Configuration;

namespace EquipmentBorrowing.Infrastructure.Data;

// Reads the PostgreSQL connection string from dotnet user-secrets,
// so no password is ever stored in a file that goes into Git.
public static class DatabaseSettings
{
    // This ID only tells .NET which secrets folder to read. It is not a secret.
    private const string UserSecretsId = "c0302fa0-0844-4070-a951-b421dc204fee";

    public static string GetConnectionString(string name = "Main")
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(UserSecretsId)
            .Build();

        var connectionString = configuration.GetConnectionString(name);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{name}' was not found. " +
                "Set it with: dotnet user-secrets set \"ConnectionStrings:" + name + "\" \"<your connection string>\" " +
                "--project src/EquipmentBorrowing.Desktop");
        }

        return connectionString;
    }
}