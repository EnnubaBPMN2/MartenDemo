using JasperFx;
using Marten;
using Microsoft.Extensions.Configuration;

// 👈 add this

public record User
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
}

internal class Program
{
    private static async Task Main(string[] args)
    {
        // STEP 1 — Load configuration
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, true)
            .AddEnvironmentVariables(); // 👈 allows $env:CONN to override

        var config = builder.Build();

        // STEP 2 — Get connection string
        var connection = Environment.GetEnvironmentVariable("CONN")
                         ?? config.GetConnectionString("Postgres");

        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("No PostgreSQL connection string found in environment or appsettings.json.");
        }


        // STEP 3 — Create DocumentStore
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(connection);
            opts.AutoCreateSchemaObjects = Enum.TryParse<AutoCreate>(
                config["Marten:AutoCreateSchemaObjects"], out var autoCreate)
                ? autoCreate
                : AutoCreate.None;
        });

        // STEP 4 — Use session
        await using var session = store.LightweightSession();

        var user = new User { Id = Guid.NewGuid(), Name = "Hermann", Email = "h@ennuba.com" };
        session.Store(user);
        await session.SaveChangesAsync();

        // Query
        var dbUser = session.Query<User>().FirstOrDefault(u => u.Email == "h@ennuba.com");
        Console.WriteLine($"Usuario guardado: {dbUser?.Name} - {dbUser?.Email}");
    }
}