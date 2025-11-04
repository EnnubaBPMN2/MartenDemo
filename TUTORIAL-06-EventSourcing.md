# 🎬 Chapter 06 - Event Sourcing Fundamentals

Welcome to event sourcing! This chapter introduces a paradigm shift: instead of storing current state, we'll store a sequence of events that represent what happened.

---

## 🎯 Chapter Objectives

By the end of this chapter, you will:

- ✅ Understand event sourcing concepts
- ✅ Create events and event streams
- ✅ Build aggregates from events
- ✅ Append events to streams
- ✅ Replay events to rebuild state
- ✅ Understand when to use event sourcing

---

## 📚 What is Event Sourcing?

### **Traditional State-Based Approach**

```csharp
// Store current state only
public record BankAccount
{
    public Guid Id { get; init; }
    public decimal Balance { get; init; } // Current state
}

// Update: Replace old state with new state
var account = await session.LoadAsync<BankAccount>(id);
var updated = account with { Balance = 500 }; // Lost history!
session.Store(updated);
```

**Problems:**
- ❌ History is lost
- ❌ Can't audit changes
- ❌ Can't replay operations
- ❌ Can't answer "how did we get here?"

### **Event Sourcing Approach**

```csharp
// Store events (what happened)
public record AccountOpened(Guid AccountId, string AccountNumber, decimal InitialBalance);
public record MoneyDeposited(Guid AccountId, decimal Amount, DateTime When);
public record MoneyWithdrawn(Guid AccountId, decimal Amount, DateTime When);

// Event stream (immutable log):
// 1. AccountOpened($1000)
// 2. MoneyDeposited($500)
// 3. MoneyWithdrawn($200)
// Current balance = $1000 + $500 - $200 = $1300

// Rebuild state by replaying events
public decimal CalculateBalance(IEnumerable<object> events)
{
    decimal balance = 0;
    foreach (var @event in events)
    {
        balance = @event switch
        {
            AccountOpened e => e.InitialBalance,
            MoneyDeposited e => balance + e.Amount,
            MoneyWithdrawn e => balance - e.Amount,
            _ => balance
        };
    }
    return balance;
}
```

**Benefits:**
- ✅ Complete audit trail
- ✅ Time travel (state at any point)
- ✅ Debug by replaying events
- ✅ New projections from old events
- ✅ Natural fit for event-driven systems

---

## 🧱 Core Concepts

### **1. Event**

An **immutable fact** that something happened in the past.

```csharp
// ✅ GOOD - Past tense, immutable
public record AccountOpened(Guid AccountId, string AccountNumber, decimal InitialBalance);
public record MoneyDeposited(Guid AccountId, decimal Amount, DateTime DepositedAt);

// ❌ BAD - Present tense, suggests command
public record OpenAccount(string AccountNumber);
public record DepositMoney(decimal Amount);
```

**Event Characteristics:**
- **Immutable** - Never changes after creation
- **Past tense** - Describes what happened
- **Domain language** - Business terms
- **Complete** - All relevant data included

### **2. Event Stream**

An **ordered sequence of events** for a single entity.

```
Stream: account-123
├─ [1] AccountOpened
├─ [2] MoneyDeposited
├─ [3] MoneyWithdrawn
└─ [4] MoneyDeposited
```

**Stream Characteristics:**
- Identified by stream ID (typically aggregate ID)
- Events are ordered (version/sequence)
- Append-only (never modify/delete)
- Complete history of entity

### **3. Aggregate**

A **consistency boundary** that produces and handles events.

```csharp
public class BankAccount
{
    public Guid Id { get; private set; }
    public string AccountNumber { get; private set; } = "";
    public decimal Balance { get; private set; }

    // Apply events to build state
    public void Apply(AccountOpened e)
    {
        Id = e.AccountId;
        AccountNumber = e.AccountNumber;
        Balance = e.InitialBalance;
    }

    public void Apply(MoneyDeposited e)
    {
        Balance += e.Amount;
    }

    public void Apply(MoneyWithdrawn e)
    {
        Balance -= e.Amount;
    }
}
```

---

## 💻 Implementing Event Sourcing with Marten

### **Step 1: Define Events**

```csharp
// Events should be in a dedicated namespace/folder
namespace MartenDemo.Events
{
    // Account lifecycle events
    public record AccountOpened(
        Guid AccountId,
        string AccountNumber,
        string OwnerName,
        decimal InitialBalance,
        DateTime OpenedAt
    );

    public record MoneyDeposited(
        Guid AccountId,
        decimal Amount,
        string Description,
        DateTime DepositedAt
    );

    public record MoneyWithdrawn(
        Guid AccountId,
        decimal Amount,
        string Description,
        DateTime WithdrawnAt
    );

    public record AccountClosed(
        Guid AccountId,
        decimal FinalBalance,
        DateTime ClosedAt
    );
}
```

### **Step 2: Configure Marten for Event Sourcing**

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Event sourcing is enabled by default
    // But you can configure it:

    // Set event schema (optional)
    opts.Events.DatabaseSchemaName = "events";

    // Register event types (optional - auto-discovery works)
    opts.Events.AddEventType<AccountOpened>();
    opts.Events.AddEventType<MoneyDeposited>();
    opts.Events.AddEventType<MoneyWithdrawn>();
});
```

**What Marten Creates:**

```sql
-- Event storage table
CREATE TABLE events.mt_events (
    seq_id BIGSERIAL PRIMARY KEY,           -- Global sequence
    id UUID UNIQUE NOT NULL,                -- Event ID
    stream_id UUID NOT NULL,                -- Aggregate ID
    version INTEGER NOT NULL,               -- Position in stream
    data JSONB NOT NULL,                    -- Event data
    type VARCHAR NOT NULL,                  -- Event type
    timestamp TIMESTAMP DEFAULT now(),
    ...
);

-- Stream metadata
CREATE TABLE events.mt_streams (
    id UUID PRIMARY KEY,                    -- Stream ID
    type VARCHAR,                           -- Aggregate type
    version INTEGER,                        -- Current version
    timestamp TIMESTAMP,
    ...
);
```

### **Step 3: Append Events to Stream**

```csharp
public static async Task OpenNewAccount(
    IDocumentStore store,
    Guid accountId,
    string accountNumber,
    string ownerName,
    decimal initialBalance)
{
    await using var session = store.LightweightSession();

    // Start a new stream
    session.Events.StartStream<BankAccount>(
        accountId, // Stream ID (aggregate ID)
        new AccountOpened(
            accountId,
            accountNumber,
            ownerName,
            initialBalance,
            DateTime.UtcNow
        )
    );

    await session.SaveChangesAsync();

    Console.WriteLine($"Account {accountNumber} opened with ${initialBalance}");
}
```

### **Step 4: Append More Events**

```csharp
public static async Task DepositMoney(
    IDocumentStore store,
    Guid accountId,
    decimal amount,
    string description)
{
    await using var session = store.LightweightSession();

    // Append event to existing stream
    session.Events.Append(
        accountId, // Stream ID
        new MoneyDeposited(
            accountId,
            amount,
            description,
            DateTime.UtcNow
        )
    );

    await session.SaveChangesAsync();

    Console.WriteLine($"Deposited ${amount}");
}

public static async Task WithdrawMoney(
    IDocumentStore store,
    Guid accountId,
    decimal amount,
    string description)
{
    await using var session = store.LightweightSession();

    session.Events.Append(
        accountId,
        new MoneyWithdrawn(
            accountId,
            amount,
            description,
            DateTime.UtcNow
        )
    );

    await session.SaveChangesAsync();

    Console.WriteLine($"Withdrew ${amount}");
}
```

### **Step 5: Read Events from Stream**

```csharp
public static async Task ShowAccountHistory(IDocumentStore store, Guid accountId)
{
    await using var session = store.LightweightSession();

    // Fetch all events for this stream
    var events = await session.Events.FetchStreamAsync(accountId);

    Console.WriteLine($"Account History (Stream: {accountId}):");
    Console.WriteLine(new string('-', 50));

    foreach (var evt in events)
    {
        Console.WriteLine($"[{evt.Version}] {evt.EventType} at {evt.Timestamp}");

        switch (evt.Data)
        {
            case AccountOpened e:
                Console.WriteLine($"  Opened: {e.AccountNumber}, Initial: ${e.InitialBalance}");
                break;
            case MoneyDeposited e:
                Console.WriteLine($"  Deposited: ${e.Amount} - {e.Description}");
                break;
            case MoneyWithdrawn e:
                Console.WriteLine($"  Withdrawn: ${e.Amount} - {e.Description}");
                break;
        }
    }
}
```

---

## 🏗️ Building Aggregates

An **aggregate** rebuilds its state from events.

### **Aggregate Pattern**

```csharp
public class BankAccount
{
    // State
    public Guid Id { get; private set; }
    public string AccountNumber { get; private set; } = "";
    public string OwnerName { get; private set; } = "";
    public decimal Balance { get; private set; }
    public bool IsClosed { get; private set; }

    // Event handlers (Apply methods)
    public void Apply(AccountOpened e)
    {
        Id = e.AccountId;
        AccountNumber = e.AccountNumber;
        OwnerName = e.OwnerName;
        Balance = e.InitialBalance;
        IsClosed = false;
    }

    public void Apply(MoneyDeposited e)
    {
        Balance += e.Amount;
    }

    public void Apply(MoneyWithdrawn e)
    {
        Balance -= e.Amount;
    }

    public void Apply(AccountClosed e)
    {
        IsClosed = true;
    }
}
```

### **Load Aggregate from Events**

```csharp
public static async Task<BankAccount?> LoadAccount(IDocumentStore store, Guid accountId)
{
    await using var session = store.LightweightSession();

    // Marten automatically replays events and builds aggregate
    var account = await session.Events.AggregateStreamAsync<BankAccount>(accountId);

    return account;
}

// Usage
var account = await LoadAccount(store, accountId);
if (account != null)
{
    Console.WriteLine($"Account: {account.AccountNumber}");
    Console.WriteLine($"Owner: {account.OwnerName}");
    Console.WriteLine($"Balance: ${account.Balance}");
    Console.WriteLine($"Status: {(account.IsClosed ? "Closed" : "Active")}");
}
```

**How it works:**
1. Fetch all events for stream
2. Create empty aggregate
3. Call `Apply()` for each event in order
4. Return fully-built aggregate

---

## 🎭 Command-Event Pattern

Separate **commands** (intentions) from **events** (facts).

### **Commands (What User Wants)**

```csharp
public record OpenAccountCommand(
    string AccountNumber,
    string OwnerName,
    decimal InitialBalance
);

public record DepositMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string Description
);

public record WithdrawMoneyCommand(
    Guid AccountId,
    decimal Amount,
    string Description
);
```

### **Command Handler**

```csharp
public class BankAccountCommandHandler
{
    private readonly IDocumentStore _store;

    public BankAccountCommandHandler(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<Guid> Handle(OpenAccountCommand command)
    {
        var accountId = Guid.NewGuid();

        await using var session = _store.LightweightSession();

        session.Events.StartStream<BankAccount>(
            accountId,
            new AccountOpened(
                accountId,
                command.AccountNumber,
                command.OwnerName,
                command.InitialBalance,
                DateTime.UtcNow
            )
        );

        await session.SaveChangesAsync();

        return accountId;
    }

    public async Task Handle(DepositMoneyCommand command)
    {
        await using var session = _store.LightweightSession();

        // Load aggregate to validate business rules
        var account = await session.Events.AggregateStreamAsync<BankAccount>(command.AccountId);

        if (account == null)
        {
            throw new InvalidOperationException("Account not found");
        }

        if (account.IsClosed)
        {
            throw new InvalidOperationException("Cannot deposit to closed account");
        }

        // Append event
        session.Events.Append(
            command.AccountId,
            new MoneyDeposited(
                command.AccountId,
                command.Amount,
                command.Description,
                DateTime.UtcNow
            )
        );

        await session.SaveChangesAsync();
    }

    public async Task Handle(WithdrawMoneyCommand command)
    {
        await using var session = _store.LightweightSession();

        var account = await session.Events.AggregateStreamAsync<BankAccount>(command.AccountId);

        if (account == null)
        {
            throw new InvalidOperationException("Account not found");
        }

        if (account.IsClosed)
        {
            throw new InvalidOperationException("Cannot withdraw from closed account");
        }

        if (account.Balance < command.Amount)
        {
            throw new InvalidOperationException("Insufficient funds");
        }

        session.Events.Append(
            command.AccountId,
            new MoneyWithdrawn(
                command.AccountId,
                command.Amount,
                command.Description,
                DateTime.UtcNow
            )
        );

        await session.SaveChangesAsync();
    }
}
```

---

## 🎓 Hands-On Exercise: Complete Example

```csharp
public static async Task BankAccountDemo(IDocumentStore store)
{
    var handler = new BankAccountCommandHandler(store);

    // Open account
    var accountId = await handler.Handle(new OpenAccountCommand(
        AccountNumber: "ACC-001",
        OwnerName: "Hermann",
        InitialBalance: 1000m
    ));

    Console.WriteLine($"Account created: {accountId}");

    // Deposit money
    await handler.Handle(new DepositMoneyCommand(
        accountId,
        Amount: 500m,
        Description: "Salary"
    ));

    await handler.Handle(new DepositMoneyCommand(
        accountId,
        Amount: 250m,
        Description: "Bonus"
    ));

    // Withdraw money
    await handler.Handle(new WithdrawMoneyCommand(
        accountId,
        Amount: 300m,
        Description: "Rent payment"
    ));

    // Show history
    await ShowAccountHistory(store, accountId);

    // Load current state
    var account = await LoadAccount(store, accountId);
    Console.WriteLine($"\nCurrent Balance: ${account?.Balance}");
    // Expected: $1000 + $500 + $250 - $300 = $1450
}
```

---

## 🔍 PostgreSQL Deep Dive

### **View Event Data**

```sql
-- All events
SELECT
    seq_id,
    stream_id,
    version,
    type,
    timestamp,
    data
FROM mt_events
ORDER BY seq_id;

-- Events for specific stream
SELECT
    version,
    type,
    data,
    timestamp
FROM mt_events
WHERE stream_id = 'your-account-id-here'
ORDER BY version;

-- Event data (JSON)
SELECT
    data->>'AccountNumber' as account_number,
    data->>'InitialBalance' as initial_balance
FROM mt_events
WHERE type = 'account_opened';
```

### **Stream Metadata**

```sql
-- All streams
SELECT
    id as stream_id,
    type as aggregate_type,
    version as event_count,
    timestamp as last_modified
FROM mt_streams;
```

---

## ⚖️ When to Use Event Sourcing

### **✅ Great For:**

- **Audit requirements** - Full history needed
- **Financial systems** - Transactions, accounting
- **Complex business rules** - Need to replay decisions
- **Temporal queries** - "What was the state last month?"
- **Event-driven architectures** - Natural fit
- **Debugging** - Replay events to find bugs

### **❌ Think Twice:**

- **Simple CRUD** - Overhead not worth it
- **Reporting/Analytics** - Use projections instead
- **High event volume** - Can grow large
- **Team unfamiliarity** - Learning curve

### **Hybrid Approach**

```csharp
// Use documents for simple entities
public record Product { }
await session.Store(product);

// Use events for critical business logic
session.Events.Append(orderId, new OrderPlaced(...));
```

---

## 💡 Best Practices

### **1. Small, Focused Events**

```csharp
// ✅ GOOD - Specific events
public record EmailChanged(Guid UserId, string NewEmail);
public record NameChanged(Guid UserId, string NewName);

// ❌ BAD - Generic update event
public record UserUpdated(Guid UserId, Dictionary<string, object> Changes);
```

### **2. Include All Relevant Data**

```csharp
// ✅ GOOD - Complete context
public record MoneyWithdrawn(
    Guid AccountId,
    decimal Amount,
    string Description,
    DateTime WithdrawnAt,
    string WithdrawnBy,      // Who did it
    string TransactionId     // Reference
);

// ❌ BAD - Missing context
public record MoneyWithdrawn(decimal Amount);
```

### **3. Never Modify Events**

```csharp
// ❌ NEVER - Events are immutable facts
// Don't delete or modify events

// ✅ GOOD - Compensating events
public record OrderPlaced(...);
public record OrderCancelled(Guid OrderId, string Reason);
```

### **4. Version Your Events**

```csharp
// Version 1
public record UserCreated(Guid UserId, string Name);

// Version 2 (breaking change)
public record UserCreatedV2(Guid UserId, string FirstName, string LastName);

// Handle both versions in aggregate
public void Apply(UserCreated e) { /* old version */ }
public void Apply(UserCreatedV2 e) { /* new version */ }
```

---

## 🎓 What You've Learned

Amazing work! You now understand:

- ✅ **Event sourcing concepts** - Events vs. state
- ✅ **Event streams** - Append-only logs
- ✅ **Aggregates** - Rebuilding state from events
- ✅ **Commands vs. events** - Intentions vs. facts
- ✅ **Marten event store** - Creating and querying streams
- ✅ **When to use it** - Trade-offs and best practices

---

## 🚀 Next Steps

Ready for projections? In **[Chapter 07 - Projections](TUTORIAL-07-Projections.md)**, we'll explore:

- Building read models from events
- Live vs. async projections
- Multi-stream projections
- Custom projection logic

**Continue to [Chapter 07 - Projections](TUTORIAL-07-Projections.md) →**

---

**Questions?** Review this chapter or go back to [Chapter 05 - Concurrency](TUTORIAL-05-Concurrency.md).
