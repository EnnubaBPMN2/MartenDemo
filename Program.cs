using JasperFx;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.Configuration;
using MartenDemo.Helpers;
using MartenDemo.Models;
using MartenDemo.EventSourcing.Events;
using MartenDemo.EventSourcing.Aggregates;
using MartenDemo.EventSourcing.Projections;

// 📚 Marten Tutorial Demo Application
// This application demonstrates concepts from all tutorial chapters
// Run it to explore Marten features interactively

public record User
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
}

internal class Program
{
    private static IDocumentStore? _store;

    private static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          📚 Marten Tutorial - Interactive Demo              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Initialize DocumentStore
        _store = await InitializeStoreAsync();

        // Main menu loop
        bool running = true;
        while (running)
        {
            ShowMainMenu();
            var choice = Console.ReadLine();

            Console.Clear();

            try
            {
                switch (choice)
                {
                    case "1":
                        await Chapter01_BasicsAsync();
                        break;
                    case "2":
                        await Chapter02_QueryingAsync();
                        break;
                    case "3":
                        await Chapter03_SchemaAsync();
                        break;
                    case "4":
                        await Chapter04_SessionsAsync();
                        break;
                    case "5":
                        await Chapter05_ConcurrencyAsync();
                        break;
                    case "6":
                        await Chapter06_EventSourcingAsync();
                        break;
                    case "7":
                        await Chapter07_ProjectionsAsync();
                        break;
                    case "8":
                        await Chapter08_AdvancedAsync();
                        break;
                    case "9":
                        await DataManagementMenuAsync();
                        break;
                    case "0":
                        running = false;
                        Console.WriteLine("👋 Goodbye!");
                        break;
                    default:
                        Console.WriteLine("❌ Invalid choice. Please try again.");
                        break;
                }

                if (running && choice != "9")
                {
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                    Console.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
    }

    static void ShowMainMenu()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("                     📖 TUTORIAL CHAPTERS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("  1️⃣  Chapter 01 - Document Database Basics");
        Console.WriteLine("  2️⃣  Chapter 02 - Advanced Querying");
        Console.WriteLine("  3️⃣  Chapter 03 - Identity & Schema Management");
        Console.WriteLine("  4️⃣  Chapter 04 - Sessions & Unit of Work");
        Console.WriteLine("  5️⃣  Chapter 05 - Optimistic Concurrency");
        Console.WriteLine("  6️⃣  Chapter 06 - Event Sourcing");
        Console.WriteLine("  7️⃣  Chapter 07 - Projections & Read Models");
        Console.WriteLine("  8️⃣  Chapter 08 - Advanced Topics");
        Console.WriteLine();
        Console.WriteLine("  9️⃣  Data Management (Seed / Reset)");
        Console.WriteLine("  0️⃣  Exit");
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.Write("\n👉 Select a chapter (0-9): ");
    }

    static async Task<IDocumentStore> InitializeStoreAsync()
    {
        Console.WriteLine("🔧 Initializing Marten DocumentStore...");

        // Load configuration
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, true)
            .AddEnvironmentVariables();

        var config = builder.Build();

        // Get connection string
        var connection = Environment.GetEnvironmentVariable("CONN")
                         ?? config.GetConnectionString("Postgres");

        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("No PostgreSQL connection string found in environment or appsettings.json.");
        }

        // Create DocumentStore with full configuration
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(connection);

            // Schema management
            opts.AutoCreateSchemaObjects = Enum.TryParse<AutoCreate>(
                config["Marten:AutoCreateSchemaObjects"], out var autoCreate)
                ? autoCreate
                : AutoCreate.None;

            // Document configuration
            opts.Schema.For<User>()
                .Index(x => x.Email);

            opts.Schema.For<Product>()
                .Index(x => x.SKU)
                .Index(x => x.Price);

            // Event sourcing configuration
            opts.Events.DatabaseSchemaName = "public";

            // Register projections
            opts.Projections.Add<AccountBalanceProjection>(ProjectionLifecycle.Inline);
            opts.Projections.Add<TransactionHistoryProjection>(ProjectionLifecycle.Inline);
        });

        Console.WriteLine("✅ DocumentStore initialized successfully\n");

        return store;
    }

    // ═══════════════════════════════════════════════════════════════
    // CHAPTER 01: Document Database Basics
    // ═══════════════════════════════════════════════════════════════
    static async Task Chapter01_BasicsAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("       📖 CHAPTER 01: Document Database Basics");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        await using var session = _store!.LightweightSession();

        // CREATE
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Hermann Tutorial",
            Email = "hermann.tutorial@example.com"
        };

        Console.WriteLine("1️⃣  Creating a new user...");
        session.Store(user);
        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Created: {user.Name} ({user.Email})");
        Console.WriteLine($"   ID: {user.Id}\n");

        // READ
        Console.WriteLine("2️⃣  Loading user by ID...");
        var loadedUser = await session.LoadAsync<User>(user.Id);
        Console.WriteLine($"✅ Loaded: {loadedUser?.Name}\n");

        // UPDATE
        Console.WriteLine("3️⃣  Updating user...");
        var updatedUser = user with { Name = "Hermann Updated" };
        session.Store(updatedUser);
        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Updated: {updatedUser.Name}\n");

        // QUERY
        Console.WriteLine("4️⃣  Querying users...");
        var users = await session.Query<User>()
            .Where(u => u.Email.Contains("tutorial"))
            .ToListAsync();
        Console.WriteLine($"✅ Found {users.Count} user(s) with 'tutorial' in email\n");

        // DELETE
        Console.WriteLine("5️⃣  Deleting user...");
        session.Delete<User>(user.Id);
        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Deleted user {user.Id}");
    }

    // ═══════════════════════════════════════════════════════════════
    // CHAPTER 02: Advanced Querying
    // ═══════════════════════════════════════════════════════════════
    static async Task Chapter02_QueryingAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("        🔍 CHAPTER 02: Advanced Querying");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        await using var session = _store!.LightweightSession();

        // Ensure we have some data
        var existingCount = await session.Query<User>().CountAsync();
        if (existingCount == 0)
        {
            Console.WriteLine("📝 No users found. Creating sample data...\n");
            await DataSeeder.SeedUsersAsync(_store, 10);
        }

        // 1. Filtering
        Console.WriteLine("1️⃣  Filtering users...");
        var exampleUsers = await session.Query<User>()
            .Where(u => u.Email.EndsWith("example.com"))
            .ToListAsync();
        Console.WriteLine($"✅ Found {exampleUsers.Count} users with @example.com\n");

        // 2. Ordering and Paging
        Console.WriteLine("2️⃣  Ordering and paging...");
        var page = await session.Query<User>()
            .OrderBy(u => u.Name)
            .Skip(0)
            .Take(5)
            .ToListAsync();
        Console.WriteLine($"✅ Retrieved page 1 with {page.Count} users:");
        foreach (var user in page)
        {
            Console.WriteLine($"   - {user.Name} ({user.Email})");
        }
        Console.WriteLine();

        // 3. Aggregations
        Console.WriteLine("3️⃣  Aggregations...");
        var totalUsers = await session.Query<User>().CountAsync();
        var hasUsers = await session.Query<User>().AnyAsync();
        Console.WriteLine($"✅ Total users: {totalUsers}");
        Console.WriteLine($"✅ Has users: {hasUsers}\n");

        // 4. Complex queries
        Console.WriteLine("4️⃣  Complex query...");
        var filtered = await session.Query<User>()
            .Where(u => u.Name.StartsWith("A") || u.Name.StartsWith("B"))
            .OrderBy(u => u.Name)
            .ToListAsync();
        Console.WriteLine($"✅ Found {filtered.Count} users starting with A or B");
    }

    // ═══════════════════════════════════════════════════════════════
    // CHAPTER 03: Identity & Schema
    // ═══════════════════════════════════════════════════════════════
    static async Task Chapter03_SchemaAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("      🏗️  CHAPTER 03: Identity & Schema Management");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        Console.WriteLine("📋 Schema information:");
        Console.WriteLine($"   - User table: mt_doc_user");
        Console.WriteLine($"   - Indexed fields: Email");
        Console.WriteLine($"   - ID Strategy: Client-generated GUIDs\n");

        await using var session = _store!.LightweightSession();

        // Demonstrate ID generation
        Console.WriteLine("1️⃣  Creating document with custom ID...");
        var customId = Guid.NewGuid();
        var user = new User
        {
            Id = customId,
            Name = "Custom ID User",
            Email = "custom@example.com"
        };
        session.Store(user);
        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Created user with ID: {customId}\n");

        // Query using indexed field
        Console.WriteLine("2️⃣  Querying using indexed field (Email)...");
        var byEmail = await session.Query<User>()
            .FirstOrDefaultAsync(u => u.Email == "custom@example.com");
        Console.WriteLine($"✅ Found: {byEmail?.Name} (fast lookup via index)\n");

        // Cleanup
        session.Delete<User>(customId);
        await session.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    // CHAPTER 04: Sessions & Unit of Work
    // ═══════════════════════════════════════════════════════════════
    static async Task Chapter04_SessionsAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("       🔄 CHAPTER 04: Sessions & Unit of Work");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        Console.WriteLine("1️⃣  Lightweight Session (Fast, no identity map)...");
        await using (var session = _store!.LightweightSession())
        {
            var user = new User { Id = Guid.NewGuid(), Name = "Lightweight Test", Email = "lightweight@example.com" };
            session.Store(user);
            await session.SaveChangesAsync();
            Console.WriteLine($"✅ Created user with lightweight session\n");

            // Cleanup
            session.Delete<User>(user.Id);
            await session.SaveChangesAsync();
        }

        Console.WriteLine("2️⃣  Identity Map Session (Ensures single instance)...");
        await using (var session = _store!.OpenSession())
        {
            var user = new User { Id = Guid.NewGuid(), Name = "Identity Test", Email = "identity@example.com" };
            session.Store(user);
            await session.SaveChangesAsync();

            var load1 = await session.LoadAsync<User>(user.Id);
            var load2 = await session.LoadAsync<User>(user.Id);
            Console.WriteLine($"✅ Same instance? {ReferenceEquals(load1, load2)} (Identity map at work)\n");

            // Cleanup
            session.Delete<User>(user.Id);
            await session.SaveChangesAsync();
        }

        Console.WriteLine("3️⃣  Batch operations (Unit of Work pattern)...");
        await using (var session = _store!.LightweightSession())
        {
            var users = new[]
            {
                new User { Id = Guid.NewGuid(), Name = "Batch 1", Email = "batch1@example.com" },
                new User { Id = Guid.NewGuid(), Name = "Batch 2", Email = "batch2@example.com" },
                new User { Id = Guid.NewGuid(), Name = "Batch 3", Email = "batch3@example.com" }
            };

            foreach (var user in users)
            {
                session.Store(user);
            }

            await session.SaveChangesAsync(); // Single transaction
            Console.WriteLine($"✅ Created {users.Length} users in one transaction");

            // Cleanup
            foreach (var user in users)
            {
                session.Delete<User>(user.Id);
            }
            await session.SaveChangesAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CHAPTER 05: Optimistic Concurrency
    // ═══════════════════════════════════════════════════════════════
    static async Task Chapter05_ConcurrencyAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("       ⚔️  CHAPTER 05: Optimistic Concurrency Control");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        var userId = Guid.NewGuid();

        // Create initial user
        await using (var session = _store!.LightweightSession())
        {
            var user = new User { Id = userId, Name = "Concurrency Test", Email = "concurrency@example.com" };
            session.Store(user);
            await session.SaveChangesAsync();
            Console.WriteLine("✅ Created test user\n");
        }

        // Simulate concurrent update attempt
        Console.WriteLine("1️⃣  Attempting concurrent updates with concurrency check...");

        var task1 = Task.Run(async () =>
        {
            await using var session = _store!.LightweightSession();
            session.UseOptimisticConcurrency(); // Enable concurrency check

            var user = await session.LoadAsync<User>(userId);
            await Task.Delay(100); // Simulate processing time

            var updated = user! with { Name = "Updated by Task 1" };
            session.Store(updated);

            try
            {
                await session.SaveChangesAsync();
                return "Task 1: Success ✅";
            }
            catch (Marten.Exceptions.ConcurrencyException)
            {
                return "Task 1: Conflict detected ❌";
            }
        });

        var task2 = Task.Run(async () =>
        {
            await using var session = _store!.LightweightSession();
            session.UseOptimisticConcurrency(); // Enable concurrency check

            var user = await session.LoadAsync<User>(userId);
            await Task.Delay(100); // Simulate processing time

            var updated = user! with { Name = "Updated by Task 2" };
            session.Store(updated);

            try
            {
                await session.SaveChangesAsync();
                return "Task 2: Success ✅";
            }
            catch (Marten.Exceptions.ConcurrencyException)
            {
                return "Task 2: Conflict detected ❌";
            }
        });

        var results = await Task.WhenAll(task1, task2);
        foreach (var result in results)
        {
            Console.WriteLine($"   {result}");
        }
        Console.WriteLine("\n💡 One task succeeded, one detected conflict (as expected)\n");

        // Cleanup
        await using (var session = _store!.LightweightSession())
        {
            session.Delete<User>(userId);
            await session.SaveChangesAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CHAPTER 06: Event Sourcing
    // ═══════════════════════════════════════════════════════════════
    static async Task Chapter06_EventSourcingAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("        🎬 CHAPTER 06: Event Sourcing Fundamentals");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        var accountId = Guid.NewGuid();
        var accountNumber = $"ACC-DEMO-{DateTime.Now:yyyyMMddHHmmss}";

        await using var session = _store!.LightweightSession();

        // 1. Open account (start stream)
        Console.WriteLine("1️⃣  Opening bank account (start event stream)...");
        session.Events.StartStream<BankAccount>(
            accountId,
            new AccountOpened(accountId, accountNumber, "Demo User", 1000m, DateTime.UtcNow)
        );
        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Account opened: {accountNumber} with $1000\n");

        // 2. Deposit money
        Console.WriteLine("2️⃣  Depositing money...");
        session.Events.Append(accountId, new MoneyDeposited(accountId, 500m, "Salary", DateTime.UtcNow));
        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Deposited $500\n");

        // 3. Withdraw money
        Console.WriteLine("3️⃣  Withdrawing money...");
        session.Events.Append(accountId, new MoneyWithdrawn(accountId, 200m, "Rent payment", DateTime.UtcNow));
        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Withdrew $200\n");

        // 4. Show event stream
        Console.WriteLine("4️⃣  Event stream history:");
        var events = await session.Events.FetchStreamAsync(accountId);
        foreach (var evt in events)
        {
            Console.WriteLine($"   [{evt.Version}] {evt.EventType} at {evt.Timestamp:HH:mm:ss}");
        }
        Console.WriteLine();

        // 5. Rebuild aggregate from events
        Console.WriteLine("5️⃣  Rebuilding aggregate from events...");
        var account = await session.Events.AggregateStreamAsync<BankAccount>(accountId);
        Console.WriteLine($"✅ Current balance: ${account?.Balance}");
        Console.WriteLine($"   Expected: $1300 ($1000 + $500 - $200)\n");
    }

    // ═══════════════════════════════════════════════════════════════
    // CHAPTER 07: Projections & Read Models
    // ═══════════════════════════════════════════════════════════════
    static async Task Chapter07_ProjectionsAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("       📊 CHAPTER 07: Projections & Read Models");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        var accountId = Guid.NewGuid();
        var accountNumber = $"ACC-PROJ-{DateTime.Now:yyyyMMddHHmmss}";

        await using var session = _store!.LightweightSession();

        // Create events
        Console.WriteLine("1️⃣  Creating events...");
        session.Events.StartStream<BankAccount>(
            accountId,
            new AccountOpened(accountId, accountNumber, "Projection Demo", 2000m, DateTime.UtcNow)
        );
        session.Events.Append(accountId, new MoneyDeposited(accountId, 750m, "Bonus", DateTime.UtcNow));
        session.Events.Append(accountId, new MoneyWithdrawn(accountId, 300m, "Shopping", DateTime.UtcNow));
        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Events created for {accountNumber}\n");

        // Query inline projection (AccountBalance)
        Console.WriteLine("2️⃣  Querying inline projection (AccountBalance)...");
        var balance = await session.LoadAsync<AccountBalance>(accountId);
        Console.WriteLine($"✅ Balance projection:");
        Console.WriteLine($"   Account: {balance?.AccountNumber}");
        Console.WriteLine($"   Owner: {balance?.OwnerName}");
        Console.WriteLine($"   Balance: ${balance?.Balance}");
        Console.WriteLine($"   Last Modified: {balance?.LastModified:yyyy-MM-dd HH:mm:ss}\n");

        // Query transaction history projection
        Console.WriteLine("3️⃣  Querying transaction history projection...");
        var history = await session.LoadAsync<TransactionHistory>(accountId);
        Console.WriteLine($"✅ Transaction history ({history?.Transactions.Count} transactions):");
        foreach (var tx in history?.Transactions ?? new())
        {
            Console.WriteLine($"   {tx.Type,-12} ${tx.Amount,8:F2} - {tx.Description}");
        }
        Console.WriteLine();

        Console.WriteLine("💡 Projections updated automatically (inline mode)");
    }

    // ═══════════════════════════════════════════════════════════════
    // CHAPTER 08: Advanced Topics
    // ═══════════════════════════════════════════════════════════════
    static async Task Chapter08_AdvancedAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("        🚀 CHAPTER 08: Advanced Topics");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

        await using var session = _store!.LightweightSession();

        // Document Patching
        Console.WriteLine("1️⃣  Document Patching...");
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Name = "Patch Demo", Email = "old@example.com" };
        session.Store(user);
        await session.SaveChangesAsync();

        // Patch without loading entire document
        session.Patch<User>(userId).Set(u => u.Email, "new@example.com");
        await session.SaveChangesAsync();

        var patched = await session.LoadAsync<User>(userId);
        Console.WriteLine($"✅ Patched email: {patched?.Email}");
        Console.WriteLine($"   (Updated without loading entire document)\n");

        // Batch operations
        Console.WriteLine("2️⃣  Batch Operations...");
        var batchUsers = new[]
        {
            new User { Id = Guid.NewGuid(), Name = "Batch A", Email = "batch.a@example.com" },
            new User { Id = Guid.NewGuid(), Name = "Batch B", Email = "batch.b@example.com" },
            new User { Id = Guid.NewGuid(), Name = "Batch C", Email = "batch.c@example.com" }
        };

        foreach (var u in batchUsers)
        {
            session.Store(u);
        }
        await session.SaveChangesAsync();
        Console.WriteLine($"✅ Created {batchUsers.Length} users in single transaction\n");

        // Cleanup
        session.Delete<User>(userId);
        foreach (var u in batchUsers)
        {
            session.Delete<User>(u.Id);
        }
        await session.SaveChangesAsync();

        Console.WriteLine("💡 Review TUTORIAL-08-Advanced.md for multi-tenancy,");
        Console.WriteLine("   migrations, and production best practices");
    }

    // ═══════════════════════════════════════════════════════════════
    // DATA MANAGEMENT MENU
    // ═══════════════════════════════════════════════════════════════
    static async Task DataManagementMenuAsync()
    {
        bool inMenu = true;
        while (inMenu)
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("                   🛠️  DATA MANAGEMENT");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine("  1️⃣  Seed Sample Data (Users, Products, Bank Accounts)");
            Console.WriteLine("  2️⃣  Reset Documents (Keep Schema)");
            Console.WriteLine("  3️⃣  Reset Events");
            Console.WriteLine("  4️⃣  Complete Reset (Documents + Events)");
            Console.WriteLine("  5️⃣  View Data Statistics");
            Console.WriteLine("  0️⃣  Back to Main Menu");
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.Write("\n👉 Select an option (0-5): ");

            var choice = Console.ReadLine();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    await DataSeeder.SeedAllAsync(_store!);
                    break;
                case "2":
                    await DatabaseReset.ResetDocumentsAsync(_store!);
                    break;
                case "3":
                    await DatabaseReset.ResetEventsAsync(_store!);
                    break;
                case "4":
                    Console.Write("⚠️  Are you sure? This will delete all data (y/n): ");
                    if (Console.ReadLine()?.ToLower() == "y")
                    {
                        await DatabaseReset.CompleteResetAsync(_store!);
                    }
                    else
                    {
                        Console.WriteLine("❌ Cancelled");
                    }
                    break;
                case "5":
                    await ShowDataStatisticsAsync();
                    break;
                case "0":
                    inMenu = false;
                    Console.Clear();
                    break;
                default:
                    Console.WriteLine("❌ Invalid choice");
                    break;
            }

            if (inMenu && choice != "0")
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
    }

    static async Task ShowDataStatisticsAsync()
    {
        await using var session = _store!.LightweightSession();

        Console.WriteLine("📊 DATABASE STATISTICS");
        Console.WriteLine("─────────────────────────────────────────────────────────────");

        var userCount = await session.Query<User>().CountAsync();
        var productCount = await session.Query<Product>().CountAsync();

        // Count events
        var eventCount = await session.Events.QueryAllRawEvents().CountAsync();

        // Count projections
        var balanceCount = await session.Query<AccountBalance>().CountAsync();

        Console.WriteLine($"Users:              {userCount,10}");
        Console.WriteLine($"Products:           {productCount,10}");
        Console.WriteLine($"Events:             {eventCount,10}");
        Console.WriteLine($"Account Balances:   {balanceCount,10}");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
    }
}