# ⚔️ Chapter 05 - Optimistic Concurrency Control

In multi-user applications, concurrent updates can cause data loss. Marten provides optimistic concurrency control to detect and handle conflicts gracefully.

---

## 🎯 Chapter Objectives

By the end of this chapter, you will:

- ✅ Understand concurrency problems (lost updates)
- ✅ Implement optimistic concurrency control
- ✅ Use version tracking
- ✅ Detect and handle conflicts
- ✅ Apply appropriate concurrency strategies
- ✅ Build conflict resolution UI flows

---

## ⚠️ The Concurrency Problem

### **Lost Update Scenario**

```csharp
// Two users editing the same document

// User A loads document
var userA_session = store.LightweightSession();
var userA_doc = await userA_session.LoadAsync<BankAccount>(accountId);
// Balance: $1000

// User B loads same document
var userB_session = store.LightweightSession();
var userB_doc = await userB_session.LoadAsync<BankAccount>(accountId);
// Balance: $1000

// User A withdraws $100
userA_doc = userA_doc with { Balance = userA_doc.Balance - 100 };
userA_session.Store(userA_doc);
await userA_session.SaveChangesAsync();
// Balance now: $900

// User B withdraws $200 (based on original $1000!)
userB_doc = userB_doc with { Balance = userB_doc.Balance - 200 };
userB_session.Store(userB_doc);
await userB_session.SaveChangesAsync();
// Balance now: $800 ❌ Should be $700!

// User A's update is LOST!
```

**Result:** User A's withdrawal disappeared. Balance should be $700 but is $800.

---

## 🛡️ Optimistic Concurrency Strategies

### **1. Last Write Wins (Default)**

```csharp
// No concurrency control
// Latest write overwrites previous changes
await using var session = store.LightweightSession();
var doc = await session.LoadAsync<BankAccount>(id);
var updated = doc with { Balance = 500 };
session.Store(updated);
await session.SaveChangesAsync(); // Always succeeds
```

**When to use:** Single-user scenarios, low conflict probability

### **2. Use Version Tracking**

Marten automatically maintains a version column (`mt_version`) for each document.

```csharp
// Enable concurrency checking
await using var session = store.LightweightSession();

var doc = await session.LoadAsync<BankAccount>(id);
var updated = doc with { Balance = doc.Balance - 100 };

// Check version hasn't changed
session.Store(updated);
session.UseOptimisticConcurrency(); // Enable checking

try
{
    await session.SaveChangesAsync();
    Console.WriteLine("Update successful");
}
catch (ConcurrencyException ex)
{
    Console.WriteLine("Conflict detected! Document was modified by another user.");
    // Handle conflict
}
```

---

## 📦 Implementing Concurrency Control

### **Method 1: Session-Level (All Documents)**

```csharp
public static async Task SessionLevelConcurrency(IDocumentStore store, Guid accountId)
{
    await using var session = store.LightweightSession();

    // Enable for all operations in this session
    session.UseOptimisticConcurrency();

    var account = await session.LoadAsync<BankAccount>(accountId);
    var updated = account with { Balance = account.Balance - 100 };

    session.Store(updated);

    try
    {
        await session.SaveChangesAsync();
    }
    catch (ConcurrencyException)
    {
        // Conflict detected
        Console.WriteLine("Update failed - document was modified");
    }
}
```

### **Method 2: Document-Level (Specific Documents)**

```csharp
public static async Task DocumentLevelConcurrency(IDocumentStore store, Guid accountId)
{
    await using var session = store.LightweightSession();

    var account = await session.LoadAsync<BankAccount>(accountId);
    var updated = account with { Balance = account.Balance - 100 };

    // Enable concurrency check for this specific document
    session.Store(updated);
    session.UseOptimisticConcurrency(updated);

    try
    {
        await session.SaveChangesAsync();
    }
    catch (ConcurrencyException)
    {
        Console.WriteLine("Conflict detected");
    }
}
```

### **Method 3: Global Configuration**

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Enable optimistic concurrency for all sessions
    opts.UseOptimisticConcurrency(true);

    // Or per document type
    opts.Schema.For<BankAccount>()
        .UseOptimisticConcurrency(true);
});

// Now all operations on BankAccount check versions automatically
await using var session = store.LightweightSession();
var account = await session.LoadAsync<BankAccount>(id);
var updated = account with { Balance = 500 };
session.Store(updated);
await session.SaveChangesAsync(); // Throws if version changed
```

---

## 🔄 Understanding Version Tracking

### **How Versions Work**

Every document has an `mt_version` column (UUID):

```sql
-- PostgreSQL schema
CREATE TABLE mt_doc_bankaccount (
    id UUID PRIMARY KEY,
    data JSONB NOT NULL,
    mt_version UUID NOT NULL DEFAULT gen_random_uuid(),
    ...
);
```

**Update Process:**
1. Load document → Remember version
2. Modify document
3. Update: `SET data = ?, mt_version = gen_random_uuid() WHERE id = ? AND mt_version = ?`
4. If no rows affected → Version mismatch → Throw exception

### **Viewing Versions**

```sql
-- See document versions
SELECT
    id,
    data->>'Balance' as balance,
    mt_version
FROM mt_doc_bankaccount;

-- Example output:
-- id                                   | balance | mt_version
-- 550e8400-e29b-41d4-a716-446655440000 | 1000    | a1b2c3d4-e5f6-...
```

### **Version in Application**

```csharp
// Marten doesn't expose version in your document
// It's tracked internally in the database
// You don't need to add a Version property to your record/class
```

---

## 🎯 Conflict Resolution Strategies

### **1. Retry Logic**

```csharp
public static async Task<bool> WithdrawWithRetry(
    IDocumentStore store,
    Guid accountId,
    decimal amount,
    int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await using var session = store.LightweightSession();
            session.UseOptimisticConcurrency();

            var account = await session.LoadAsync<BankAccount>(accountId);

            if (account == null || account.Balance < amount)
            {
                return false; // Insufficient funds
            }

            var updated = account with { Balance = account.Balance - amount };
            session.Store(updated);
            await session.SaveChangesAsync();

            return true; // Success
        }
        catch (ConcurrencyException)
        {
            if (attempt == maxRetries)
            {
                Console.WriteLine("Max retries exceeded");
                throw;
            }

            Console.WriteLine($"Conflict detected, retrying ({attempt}/{maxRetries})...");
            await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt)); // Exponential backoff
        }
    }

    return false;
}
```

### **2. Reload and Merge**

```csharp
public static async Task<BankAccount> UpdateWithMerge(
    IDocumentStore store,
    Guid accountId,
    Func<BankAccount, BankAccount> updateFunc)
{
    while (true)
    {
        await using var session = store.LightweightSession();
        session.UseOptimisticConcurrency();

        var account = await session.LoadAsync<BankAccount>(accountId);
        var updated = updateFunc(account);

        session.Store(updated);

        try
        {
            await session.SaveChangesAsync();
            return updated;
        }
        catch (ConcurrencyException)
        {
            // Reload latest version and try again
            Console.WriteLine("Conflict - reloading and retrying");
        }
    }
}

// Usage
var result = await UpdateWithMerge(store, accountId, account =>
    account with { Balance = account.Balance - 100 }
);
```

### **3. User Confirmation (UI Pattern)**

```csharp
public record ConflictResolution<T>
{
    public T OriginalVersion { get; init; }
    public T YourVersion { get; init; }
    public T CurrentVersion { get; init; }
}

public static async Task<ConflictResolution<BankAccount>?> DetectConflict(
    IDocumentStore store,
    Guid accountId,
    BankAccount yourVersion)
{
    await using var session = store.LightweightSession();

    var currentVersion = await session.LoadAsync<BankAccount>(accountId);

    // Compare versions (simplified - you'd track version IDs in reality)
    if (currentVersion.Balance != yourVersion.Balance)
    {
        return new ConflictResolution<BankAccount>
        {
            OriginalVersion = yourVersion, // What user started with
            YourVersion = yourVersion,     // What user wants to save
            CurrentVersion = currentVersion // What's in DB now
        };
    }

    return null; // No conflict
}

// In UI:
// "Warning: This document was modified by another user.
//  Your balance: $800
//  Current balance: $700
//  [Overwrite] [Reload] [Cancel]"
```

### **4. Application-Level Versioning**

```csharp
public record VersionedDocument
{
    public Guid Id { get; init; }
    public int Version { get; init; } // Application-managed
    public string Data { get; init; } = "";
}

public static async Task UpdateWithAppVersion(
    IDocumentStore store,
    VersionedDocument document)
{
    await using var session = store.LightweightSession();

    var current = await session.LoadAsync<VersionedDocument>(document.Id);

    if (current.Version != document.Version)
    {
        throw new InvalidOperationException(
            $"Version conflict: Expected {document.Version}, found {current.Version}");
    }

    var updated = document with { Version = document.Version + 1 };
    session.Store(updated);
    await session.SaveChangesAsync();
}
```

---

## 🔍 Real-World Example: Bank Account

```csharp
public record BankAccount
{
    public Guid Id { get; init; }
    public string AccountNumber { get; init; } = "";
    public decimal Balance { get; init; }
    public DateTime LastModified { get; init; }
}

public class BankAccountService
{
    private readonly IDocumentStore _store;

    public BankAccountService(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<bool> Withdraw(Guid accountId, decimal amount, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await using var session = _store.LightweightSession();
                session.UseOptimisticConcurrency();

                var account = await session.LoadAsync<BankAccount>(accountId);

                if (account == null)
                {
                    throw new InvalidOperationException("Account not found");
                }

                if (account.Balance < amount)
                {
                    return false; // Insufficient funds
                }

                var updated = account with
                {
                    Balance = account.Balance - amount,
                    LastModified = DateTime.UtcNow
                };

                session.Store(updated);
                await session.SaveChangesAsync();

                Console.WriteLine($"Withdrawal successful: ${amount} (New balance: ${updated.Balance})");
                return true;
            }
            catch (ConcurrencyException)
            {
                if (attempt == maxRetries)
                {
                    Console.WriteLine("Too many concurrent updates - please try again");
                    throw;
                }

                Console.WriteLine($"Concurrent update detected, retrying ({attempt}/{maxRetries})...");
                await Task.Delay(100 * attempt);
            }
        }

        return false;
    }

    public async Task<bool> Transfer(Guid fromAccountId, Guid toAccountId, decimal amount)
    {
        // Both accounts must be updated atomically with concurrency checks
        await using var session = _store.LightweightSession();
        session.UseOptimisticConcurrency();

        try
        {
            var fromAccount = await session.LoadAsync<BankAccount>(fromAccountId);
            var toAccount = await session.LoadAsync<BankAccount>(toAccountId);

            if (fromAccount == null || toAccount == null)
            {
                throw new InvalidOperationException("Account not found");
            }

            if (fromAccount.Balance < amount)
            {
                return false; // Insufficient funds
            }

            var updatedFrom = fromAccount with
            {
                Balance = fromAccount.Balance - amount,
                LastModified = DateTime.UtcNow
            };

            var updatedTo = toAccount with
            {
                Balance = toAccount.Balance + amount,
                LastModified = DateTime.UtcNow
            };

            session.Store(updatedFrom);
            session.Store(updatedTo);
            await session.SaveChangesAsync();

            Console.WriteLine($"Transfer successful: ${amount}");
            return true;
        }
        catch (ConcurrencyException)
        {
            Console.WriteLine("Transfer failed due to concurrent update");
            return false;
        }
    }
}
```

---

## 🎓 Hands-On Exercises

### **Exercise 1: Simulate Concurrent Updates**

```csharp
public static async Task SimulateConcurrentUpdates(IDocumentStore store)
{
    var accountId = Guid.NewGuid();

    // Create initial account
    await using (var session = store.LightweightSession())
    {
        var account = new BankAccount
        {
            Id = accountId,
            AccountNumber = "12345",
            Balance = 1000,
            LastModified = DateTime.UtcNow
        };
        session.Store(account);
        await session.SaveChangesAsync();
    }

    // Simulate two concurrent users
    var task1 = Task.Run(async () =>
    {
        await using var session = store.LightweightSession();
        session.UseOptimisticConcurrency();

        var account = await session.LoadAsync<BankAccount>(accountId);
        await Task.Delay(100); // Simulate user thinking

        var updated = account with { Balance = account.Balance - 100 };
        session.Store(updated);

        try
        {
            await session.SaveChangesAsync();
            Console.WriteLine("User 1: Withdrawal successful");
        }
        catch (ConcurrencyException)
        {
            Console.WriteLine("User 1: Conflict detected!");
        }
    });

    var task2 = Task.Run(async () =>
    {
        await using var session = store.LightweightSession();
        session.UseOptimisticConcurrency();

        var account = await session.LoadAsync<BankAccount>(accountId);
        await Task.Delay(100); // Simulate user thinking

        var updated = account with { Balance = account.Balance - 200 };
        session.Store(updated);

        try
        {
            await session.SaveChangesAsync();
            Console.WriteLine("User 2: Withdrawal successful");
        }
        catch (ConcurrencyException)
        {
            Console.WriteLine("User 2: Conflict detected!");
        }
    });

    await Task.WhenAll(task1, task2);

    // Check final balance
    await using (var session = store.LightweightSession())
    {
        var account = await session.LoadAsync<BankAccount>(accountId);
        Console.WriteLine($"Final balance: ${account.Balance}");
    }
}
```

### **Exercise 2: Build Retry Logic**

```csharp
public static async Task<T> RetryOnConflict<T>(
    Func<Task<T>> operation,
    int maxRetries = 3,
    int delayMs = 100)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            return await operation();
        }
        catch (ConcurrencyException) when (attempt < maxRetries)
        {
            Console.WriteLine($"Retry {attempt}/{maxRetries}");
            await Task.Delay(delayMs * attempt);
        }
    }

    // Last attempt without catch
    return await operation();
}

// Usage
var result = await RetryOnConflict(async () =>
{
    await using var session = store.LightweightSession();
    session.UseOptimisticConcurrency();

    var account = await session.LoadAsync<BankAccount>(accountId);
    var updated = account with { Balance = account.Balance - 50 };
    session.Store(updated);
    await session.SaveChangesAsync();

    return updated;
});
```

---

## 💡 Best Practices

### **1. Enable Concurrency for Critical Operations**

```csharp
// ✅ GOOD - Protect financial operations
session.UseOptimisticConcurrency();
var account = await session.LoadAsync<BankAccount>(id);
var updated = account with { Balance = account.Balance - amount };
session.Store(updated);
await session.SaveChangesAsync();

// ❌ RISKY - No protection
var account = await session.LoadAsync<BankAccount>(id);
var updated = account with { Balance = account.Balance - amount };
session.Store(updated);
await session.SaveChangesAsync(); // Could overwrite concurrent changes
```

### **2. Implement Retry Logic**

```csharp
// ✅ GOOD - Retry with exponential backoff
for (int attempt = 1; attempt <= 3; attempt++)
{
    try
    {
        // ... update logic
        await session.SaveChangesAsync();
        break;
    }
    catch (ConcurrencyException)
    {
        if (attempt == 3) throw;
        await Task.Delay(100 * attempt);
    }
}
```

### **3. Inform Users of Conflicts**

```csharp
// ✅ GOOD - User-friendly error handling
try
{
    await session.SaveChangesAsync();
}
catch (ConcurrencyException)
{
    return BadRequest(new
    {
        Error = "This record was modified by another user. Please reload and try again."
    });
}
```

### **4. Choose Appropriate Strategy**

```csharp
// Critical operations → Concurrency checks
// - Financial transactions
// - Inventory management
// - User account changes

// Low-risk operations → Last write wins
// - Logging
// - Analytics events
// - User preferences
```

---

## 📊 Concurrency Strategy Decision Tree

```
Is data loss critical?
├─ YES
│   └─ Enable optimistic concurrency
│       └─ High conflict probability?
│           ├─ YES → Implement robust retry logic
│           └─ NO → Simple retry or user notification
└─ NO
    └─ Last write wins (default)
```

---

## 🎓 What You've Learned

Outstanding! You now understand:

- ✅ **Concurrency problems** - Lost updates and why they occur
- ✅ **Optimistic concurrency** - Version-based conflict detection
- ✅ **Implementation methods** - Session, document, and global levels
- ✅ **Conflict resolution** - Retry, merge, user confirmation
- ✅ **Best practices** - When and how to use concurrency control
- ✅ **Real-world patterns** - Bank account example

---

## 🚀 Next Steps

Ready for event sourcing? In **[Chapter 06 - Event Sourcing](TUTORIAL-06-EventSourcing.md)**, we'll dive into:

- Event sourcing fundamentals
- Event streams and aggregates
- Immutable event history
- Rebuilding state from events

**Continue to [Chapter 06 - Event Sourcing](TUTORIAL-06-EventSourcing.md) →**

---

**Questions?** Review this chapter or go back to [Chapter 04 - Sessions](TUTORIAL-04-Sessions.md).
