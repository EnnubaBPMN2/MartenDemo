using Marten;
using MartenDemo.EventSourcing.Aggregates;
using MartenDemo.EventSourcing.Events;
using MartenDemo.Models;

namespace MartenDemo.Helpers;

public static class DataSeeder
{
    /// <summary>
    ///     Seed sample users
    /// </summary>
    public static async Task SeedUsersAsync(IDocumentStore store, int count = 10)
    {
        await using var session = store.LightweightSession();

        var users = new List<User>();
        var names = new[] { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry", "Iris", "Jack" };
        var domains = new[] { "example.com", "test.com", "demo.com" };

        for (var i = 0; i < Math.Min(count, names.Length); i++)
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Name = names[i],
                Email = $"{names[i].ToLower()}@{domains[i % domains.Length]}"
            });

        foreach (var user in users) session.Store(user);

        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Seeded {users.Count} users");
    }

    /// <summary>
    ///     Seed sample products
    /// </summary>
    public static async Task SeedProductsAsync(IDocumentStore store, int count = 20)
    {
        await using var session = store.LightweightSession();

        var productNames = new[]
        {
            "Laptop", "Mouse", "Keyboard", "Monitor", "Headphones",
            "Webcam", "Microphone", "Speakers", "USB Hub", "Cable",
            "Desk", "Chair", "Lamp", "Notebook", "Pen",
            "Backpack", "Water Bottle", "Coffee Mug", "Plant", "Calendar"
        };

        var products = new List<Product>();

        for (var i = 0; i < Math.Min(count, productNames.Length); i++)
            products.Add(new Product
            {
                Id = Guid.NewGuid(),
                SKU = $"PRD-{1000 + i}",
                Name = productNames[i],
                Description = $"High quality {productNames[i].ToLower()} for professionals",
                Price = (decimal)Math.Round(Random.Shared.NextDouble() * 500 + 10, 2),
                StockQuantity = Random.Shared.Next(0, 100),
                Tags = GetRandomTags(),
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 365))
            });

        foreach (var product in products) session.Store(product);

        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Seeded {products.Count} products");
    }

    /// <summary>
    ///     Seed sample bank accounts (event sourcing)
    /// </summary>
    public static async Task SeedBankAccountsAsync(IDocumentStore store, int count = 5)
    {
        var names = new[] { "Hermann Smith", "Alice Johnson", "Bob Williams", "Charlie Brown", "Diana Davis" };
        var descriptions = new[] { "Salary", "Bonus", "Rent", "Groceries", "Utilities", "Shopping" };

        for (var i = 0; i < Math.Min(count, names.Length); i++)
        {
            await using var session = store.LightweightSession();

            var accountId = Guid.NewGuid();
            var accountNumber = $"ACC-{10000 + i}";
            var ownerName = names[i];
            var initialBalance = Random.Shared.Next(500, 5000);

            // Open account
            session.Events.StartStream<BankAccount>(
                accountId,
                new AccountOpened(
                    accountId,
                    accountNumber,
                    ownerName,
                    initialBalance,
                    DateTime.UtcNow.AddDays(-Random.Shared.Next(30, 365))
                )
            );

            // Add random transactions
            var transactionCount = Random.Shared.Next(3, 10);
            for (var j = 0; j < transactionCount; j++)
            {
                var amount = Random.Shared.Next(50, 500);
                var description = descriptions[Random.Shared.Next(descriptions.Length)];

                if (Random.Shared.Next(0, 2) == 0)
                    // Deposit
                    session.Events.Append(
                        accountId,
                        new MoneyDeposited(
                            accountId,
                            amount,
                            description,
                            DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30))
                        )
                    );
                else
                    // Withdrawal
                    session.Events.Append(
                        accountId,
                        new MoneyWithdrawn(
                            accountId,
                            amount,
                            description,
                            DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 30))
                        )
                    );
            }

            await session.SaveChangesAsync();
        }

        Console.WriteLine($"✅ Seeded {Math.Min(count, names.Length)} bank accounts with transactions");
    }

    /// <summary>
    ///     Seed all sample data
    /// </summary>
    public static async Task SeedAllAsync(IDocumentStore store)
    {
        Console.WriteLine("🌱 Seeding sample data...");

        await SeedUsersAsync(store);
        await SeedProductsAsync(store);
        await SeedBankAccountsAsync(store);

        Console.WriteLine("✅ All sample data seeded");
    }

    private static List<string> GetRandomTags()
    {
        var allTags = new[]
            { "electronics", "office", "home", "tech", "productivity", "comfort", "wireless", "ergonomic" };
        var count = Random.Shared.Next(1, 4);
        return allTags.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
    }
}