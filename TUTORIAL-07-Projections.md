# 📊 Chapter 07 - Projections & Read Models

Projections transform event streams into queryable read models. This chapter shows you how to build optimized views of your data from events.

---

## 🎯 Chapter Objectives

By the end of this chapter, you will:

- ✅ Understand projections and read models
- ✅ Create inline projections
- ✅ Build live aggregations
- ✅ Implement async projections
- ✅ Handle multi-stream projections
- ✅ Query projected views efficiently

---

## 📚 What are Projections?

### **The Problem**

Event streams are great for **writing**, but not ideal for **reading**:

```csharp
// To get account balance, replay ALL events
var events = await session.Events.FetchStreamAsync(accountId);
decimal balance = 0;
foreach (var evt in events)
{
    balance = evt.Data switch
    {
        AccountOpened e => e.InitialBalance,
        MoneyDeposited e => balance + e.Amount,
        MoneyWithdrawn e => balance - e.Amount,
        _ => balance
    };
}
// Slow for many events!
```

### **The Solution: Projections**

Build **read models** (projections) from events:

```csharp
// Read model (document)
public record AccountBalance
{
    public Guid Id { get; init; }
    public string AccountNumber { get; init; } = "";
    public string OwnerName { get; init; } = "";
    public decimal Balance { get; init; }
    public DateTime LastModified { get; init; }
}

// Now query instantly
var balance = await session.LoadAsync<AccountBalance>(accountId);
Console.WriteLine($"Balance: ${balance?.Balance}");
```

**Benefits:**
- ✅ Fast queries (no event replay)
- ✅ Optimized for reads
- ✅ Multiple views from same events
- ✅ Separate read/write models (CQRS)

---

## 🎭 Types of Projections

### **1. Live Aggregation**

Build aggregate on-demand from events (no storage).

```csharp
// Already covered in Chapter 06
var account = await session.Events.AggregateStreamAsync<BankAccount>(accountId);
```

**Pros:** Always up-to-date, no storage
**Cons:** Slower (replays events every time)

### **2. Inline Projections**

Update projection **synchronously** when events are appended.

```csharp
// Projection updates in same transaction as event append
session.Events.Append(accountId, new MoneyDeposited(...));
await session.SaveChangesAsync();
// AccountBalance document updated immediately
```

**Pros:** Always up-to-date, fast reads
**Cons:** Slows down writes slightly

### **3. Async Projections**

Update projection **asynchronously** via background process.

```csharp
// Events appended immediately
session.Events.Append(accountId, new MoneyDeposited(...));
await session.SaveChangesAsync();

// Projection updated by daemon (seconds later)
// AccountBalance eventually consistent
```

**Pros:** Fast writes, doesn't block
**Cons:** Eventually consistent (slight delay)

---

## 💻 Inline Projections

### **Single-Stream Projection**

Build a read model from one event stream.

```csharp
// Read model
public class AccountBalance
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public decimal Balance { get; set; }
    public DateTime LastModified { get; set; }
}

// Projection definition
public class AccountBalanceProjection : SingleStreamProjection<AccountBalance>
{
    // Create document when stream starts
    public AccountBalance Create(AccountOpened e)
    {
        return new AccountBalance
        {
            Id = e.AccountId,
            AccountNumber = e.AccountNumber,
            OwnerName = e.OwnerName,
            Balance = e.InitialBalance,
            LastModified = e.OpenedAt
        };
    }

    // Update document when events occur
    public void Apply(MoneyDeposited e, AccountBalance view)
    {
        view.Balance += e.Amount;
        view.LastModified = e.DepositedAt;
    }

    public void Apply(MoneyWithdrawn e, AccountBalance view)
    {
        view.Balance -= e.Amount;
        view.LastModified = e.WithdrawnAt;
    }
}

// Register projection
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Register inline projection
    opts.Projections.Add<AccountBalanceProjection>(ProjectionLifecycle.Inline);
});
```

### **Using the Projection**

```csharp
public static async Task InlineProjectionDemo(IDocumentStore store)
{
    var accountId = Guid.NewGuid();

    await using var session = store.LightweightSession();

    // Append events
    session.Events.StartStream<BankAccount>(
        accountId,
        new AccountOpened(accountId, "ACC-001", "Hermann", 1000m, DateTime.UtcNow)
    );

    await session.SaveChangesAsync();
    // AccountBalance document created automatically!

    // Query the read model
    var balance = await session.LoadAsync<AccountBalance>(accountId);
    Console.WriteLine($"Balance: ${balance?.Balance}"); // $1000

    // Append more events
    session.Events.Append(accountId, new MoneyDeposited(accountId, 500m, "Salary", DateTime.UtcNow));
    await session.SaveChangesAsync();

    // Reload projection
    var updatedBalance = await session.LoadAsync<AccountBalance>(accountId);
    Console.WriteLine($"Balance: ${updatedBalance?.Balance}"); // $1500
}
```

---

## 🔄 Multi-Stream Projections

Build a read model from **multiple event streams**.

### **Example: Account Summary Dashboard**

```csharp
// Events from multiple streams
// Stream: account-1 → AccountOpened, MoneyDeposited
// Stream: account-2 → AccountOpened, MoneyWithdrawn
// Stream: account-3 → AccountOpened

// Read model: Total accounts and balance
public class BankSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TotalAccounts { get; set; }
    public decimal TotalBalance { get; set; }
    public DateTime LastUpdated { get; set; }
}

// Multi-stream projection
public class BankSummaryProjection : MultiStreamProjection<BankSummary, Guid>
{
    public BankSummaryProjection()
    {
        // Use a single document for all events
        Identity<AccountOpened>(e => Guid.Empty); // Same ID for all
        Identity<MoneyDeposited>(e => Guid.Empty);
        Identity<MoneyWithdrawn>(e => Guid.Empty);
    }

    public BankSummary Create(AccountOpened e)
    {
        return new BankSummary
        {
            Id = Guid.Empty,
            TotalAccounts = 1,
            TotalBalance = e.InitialBalance,
            LastUpdated = DateTime.UtcNow
        };
    }

    public void Apply(AccountOpened e, BankSummary view)
    {
        view.TotalAccounts++;
        view.TotalBalance += e.InitialBalance;
        view.LastUpdated = DateTime.UtcNow;
    }

    public void Apply(MoneyDeposited e, BankSummary view)
    {
        view.TotalBalance += e.Amount;
        view.LastUpdated = DateTime.UtcNow;
    }

    public void Apply(MoneyWithdrawn e, BankSummary view)
    {
        view.TotalBalance -= e.Amount;
        view.LastUpdated = DateTime.UtcNow;
    }
}

// Register
opts.Projections.Add<BankSummaryProjection>(ProjectionLifecycle.Inline);

// Query
var summary = await session.LoadAsync<BankSummary>(Guid.Empty);
Console.WriteLine($"Total Accounts: {summary?.TotalAccounts}");
Console.WriteLine($"Total Balance: ${summary?.TotalBalance}");
```

---

## 🏗️ ViewProjection (Advanced)

More control with `ViewProjection`:

```csharp
public class AccountListProjection : ViewProjection<AccountListView, Guid>
{
    public AccountListProjection()
    {
        // Project AccountOpened events
        ProjectEvent<AccountOpened>((view, evt) =>
        {
            view.Id = evt.AccountId;
            view.AccountNumber = evt.AccountNumber;
            view.OwnerName = evt.OwnerName;
            view.Status = "Active";
            view.CreatedAt = evt.OpenedAt;
        });

        // Project AccountClosed events
        ProjectEvent<AccountClosed>((view, evt) =>
        {
            view.Status = "Closed";
            view.ClosedAt = evt.ClosedAt;
        });
    }

    // Delete projection when account is closed
    public override bool ShouldDelete(AccountClosed @event)
    {
        return true; // Remove from list when closed
    }
}

public class AccountListView
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
```

---

## ⚡ Async Projections

Background processing of events for better write performance.

### **Configuration**

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Register as async projection
    opts.Projections.Add<AccountBalanceProjection>(ProjectionLifecycle.Async);
});

// Start the projection daemon
using var daemon = await store.BuildProjectionDaemonAsync();
await daemon.StartAllAsync();

// Now events are processed in background
await using var session = store.LightweightSession();
session.Events.Append(accountId, new MoneyDeposited(...));
await session.SaveChangesAsync();
// Returns immediately - projection updates asynchronously
```

### **Async Projection Daemon**

```csharp
public static async Task RunAsyncProjections(IDocumentStore store)
{
    // Build daemon
    await using var daemon = await store.BuildProjectionDaemonAsync();

    // Start all async projections
    await daemon.StartAllAsync();

    Console.WriteLine("Projection daemon started");

    // Let it run
    await Task.Delay(TimeSpan.FromMinutes(5));

    // Stop gracefully
    await daemon.StopAllAsync();

    Console.WriteLine("Projection daemon stopped");
}
```

### **When to Use Async**

**✅ Use Async When:**
- High write throughput needed
- Read lag acceptable (seconds/minutes)
- Heavy projection logic
- Reporting/analytics

**❌ Use Inline When:**
- Strong consistency required
- Low write volume
- Simple projections
- User-facing reads

---

## 🎓 Hands-On Exercise: Complete Example

### **Events**

```csharp
public record AccountOpened(Guid AccountId, string AccountNumber, string OwnerName, decimal InitialBalance, DateTime OpenedAt);
public record MoneyDeposited(Guid AccountId, decimal Amount, string Description, DateTime DepositedAt);
public record MoneyWithdrawn(Guid AccountId, decimal Amount, string Description, DateTime WithdrawnAt);
```

### **Read Models**

```csharp
// Balance projection (inline)
public class AccountBalance
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public decimal Balance { get; set; }
    public DateTime LastModified { get; set; }
}

// Transaction history (async)
public class TransactionHistory
{
    public Guid Id { get; set; } // Account ID
    public List<Transaction> Transactions { get; set; } = new();
}

public record Transaction(string Type, decimal Amount, string Description, DateTime When);
```

### **Projections**

```csharp
public class AccountBalanceProjection : SingleStreamProjection<AccountBalance>
{
    public AccountBalance Create(AccountOpened e)
    {
        return new AccountBalance
        {
            Id = e.AccountId,
            AccountNumber = e.AccountNumber,
            Balance = e.InitialBalance,
            LastModified = e.OpenedAt
        };
    }

    public void Apply(MoneyDeposited e, AccountBalance view)
    {
        view.Balance += e.Amount;
        view.LastModified = e.DepositedAt;
    }

    public void Apply(MoneyWithdrawn e, AccountBalance view)
    {
        view.Balance -= e.Amount;
        view.LastModified = e.WithdrawnAt;
    }
}

public class TransactionHistoryProjection : SingleStreamProjection<TransactionHistory>
{
    public TransactionHistory Create(AccountOpened e)
    {
        return new TransactionHistory
        {
            Id = e.AccountId,
            Transactions = new List<Transaction>
            {
                new("Opened", e.InitialBalance, "Initial deposit", e.OpenedAt)
            }
        };
    }

    public void Apply(MoneyDeposited e, TransactionHistory view)
    {
        view.Transactions.Add(new Transaction("Deposit", e.Amount, e.Description, e.DepositedAt));
    }

    public void Apply(MoneyWithdrawn e, TransactionHistory view)
    {
        view.Transactions.Add(new Transaction("Withdrawal", -e.Amount, e.Description, e.WithdrawnAt));
    }
}
```

### **Configuration**

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Balance updated immediately
    opts.Projections.Add<AccountBalanceProjection>(ProjectionLifecycle.Inline);

    // History updated async (not critical for writes)
    opts.Projections.Add<TransactionHistoryProjection>(ProjectionLifecycle.Async);
});
```

### **Usage**

```csharp
public static async Task ProjectionExample(IDocumentStore store)
{
    var accountId = Guid.NewGuid();

    // Append events
    await using (var session = store.LightweightSession())
    {
        session.Events.StartStream<BankAccount>(
            accountId,
            new AccountOpened(accountId, "ACC-001", "Hermann", 1000m, DateTime.UtcNow)
        );
        session.Events.Append(accountId, new MoneyDeposited(accountId, 500m, "Salary", DateTime.UtcNow));
        session.Events.Append(accountId, new MoneyWithdrawn(accountId, 200m, "Rent", DateTime.UtcNow));

        await session.SaveChangesAsync();
    }

    // Query inline projection (available immediately)
    await using (var session = store.LightweightSession())
    {
        var balance = await session.LoadAsync<AccountBalance>(accountId);
        Console.WriteLine($"Current Balance: ${balance?.Balance}"); // $1300
    }

    // Start async daemon to process TransactionHistory
    await using var daemon = await store.BuildProjectionDaemonAsync();
    await daemon.StartAllAsync();

    // Wait a moment for async projection
    await Task.Delay(1000);

    // Query async projection
    await using (var session = store.LightweightSession())
    {
        var history = await session.LoadAsync<TransactionHistory>(accountId);
        Console.WriteLine($"Transaction Count: {history?.Transactions.Count}");

        foreach (var tx in history?.Transactions ?? new())
        {
            Console.WriteLine($"  {tx.Type}: ${tx.Amount} - {tx.Description}");
        }
    }

    await daemon.StopAllAsync();
}
```

---

## 🔧 Rebuilding Projections

### **Why Rebuild?**

- Projection logic changed
- Data corruption
- Add new projection to existing events
- Testing

### **Rebuild Command**

```csharp
public static async Task RebuildProjections(IDocumentStore store)
{
    await using var daemon = await store.BuildProjectionDaemonAsync();

    // Rebuild specific projection
    await daemon.RebuildProjectionAsync<AccountBalanceProjection>(CancellationToken.None);

    Console.WriteLine("Projection rebuilt from all events");
}

// Or rebuild all projections
await daemon.RebuildProjectionsAsync(CancellationToken.None);
```

### **Rebuild with Progress**

```csharp
var progress = new Progress<ShardState>(state =>
{
    Console.WriteLine($"Processed {state.Sequence} events");
});

await daemon.RebuildProjectionAsync<AccountBalanceProjection>(
    CancellationToken.None,
    progress
);
```

---

## 💡 Best Practices

### **1. Separate Read and Write Models**

```csharp
// ✅ GOOD - Optimized read model
public class AccountBalanceView
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public decimal Balance { get; set; }
    // Only fields needed for queries
}

// ❌ BAD - Using aggregate for reads
var account = await session.Events.AggregateStreamAsync<BankAccount>(id);
// Slow for complex aggregates
```

### **2. Use Async for Non-Critical Reads**

```csharp
// Inline: User-facing balance (must be accurate)
opts.Projections.Add<AccountBalanceProjection>(ProjectionLifecycle.Inline);

// Async: Analytics dashboard (can be slightly delayed)
opts.Projections.Add<DailyReportProjection>(ProjectionLifecycle.Async);
```

### **3. Multiple Projections from Same Events**

```csharp
// Same events → Multiple views
opts.Projections.Add<AccountBalanceProjection>(ProjectionLifecycle.Inline);
opts.Projections.Add<TransactionHistoryProjection>(ProjectionLifecycle.Async);
opts.Projections.Add<AccountSummaryProjection>(ProjectionLifecycle.Async);

// Each optimized for different queries
```

### **4. Index Projection Fields**

```csharp
opts.Schema.For<AccountBalance>()
    .Index(x => x.AccountNumber)
    .Index(x => x.Balance);

// Fast queries on projected documents
var highBalances = await session.Query<AccountBalance>()
    .Where(x => x.Balance > 10000)
    .ToListAsync();
```

---

## 📊 Projection Lifecycle Comparison

| Feature | Inline | Async | Live |
|---------|--------|-------|------|
| **Write Speed** | ⚡⚡ Slower | ⚡⚡⚡ Fast | ⚡⚡⚡ Fast |
| **Read Speed** | ⚡⚡⚡ Instant | ⚡⚡⚡ Instant | ⚡ Slow |
| **Consistency** | Strong | Eventual | Strong |
| **Storage** | ✅ Yes | ✅ Yes | ❌ No |
| **Best For** | Critical reads | Analytics | On-demand |

---

## 🎓 What You've Learned

Phenomenal! You now understand:

- ✅ **Projections concept** - Read models from events
- ✅ **Inline projections** - Synchronous updates
- ✅ **Async projections** - Background processing
- ✅ **Multi-stream projections** - Aggregate multiple streams
- ✅ **Rebuilding** - Replay events to update projections
- ✅ **CQRS pattern** - Separate read/write models

---

## 🚀 Next Steps

Almost there! In **[Chapter 08 - Advanced Topics](TUTORIAL-08-Advanced.md)**, we'll cover:

- Multi-tenancy
- Document patching
- Schema migrations
- Production deployment
- Performance tuning
- Best practices roundup

**Continue to [Chapter 08 - Advanced Topics](TUTORIAL-08-Advanced.md) →**

---

**Questions?** Review this chapter or go back to [Chapter 06 - Event Sourcing](TUTORIAL-06-EventSourcing.md).
