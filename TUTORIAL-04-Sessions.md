# 🔄 Chapter 04 - Sessions & Unit of Work

Sessions are the heart of Marten's transaction management. Understanding the different session types and when to use each is crucial for building performant applications.

---

## 🎯 Chapter Objectives

By the end of this chapter, you will:

- ✅ Understand the three session types
- ✅ Know when to use each session type
- ✅ Master transaction management
- ✅ Implement unit of work patterns
- ✅ Handle session lifetimes correctly
- ✅ Optimize session usage for performance

---

## 📚 What is a Session?

A **session** in Marten represents a **unit of work** - a transactional boundary for document operations.

**Think of it like:**
- A shopping cart (add items, then checkout)
- A database transaction (batch changes, commit together)
- A change tracker (knows what's modified)

**Session Lifecycle:**
1. **Create** - Open a session from the DocumentStore
2. **Track** - Perform operations (Store, Delete, Query)
3. **Commit** - Call `SaveChangesAsync()` to persist
4. **Dispose** - Clean up resources

---

## 🎭 The Three Session Types

Marten provides three session types, each with different capabilities and performance characteristics.

### **Quick Comparison**

| Feature | Lightweight | OpenSession | DirtyTrackedSession |
|---------|------------|-------------|---------------------|
| **Performance** | ⚡⚡⚡ Fastest | ⚡⚡ Fast | ⚡ Slower |
| **Memory** | 💾 Minimal | 💾💾 Moderate | 💾💾💾 Higher |
| **Identity Map** | ❌ No | ✅ Yes | ✅ Yes |
| **Change Tracking** | ❌ No | ❌ No | ✅ Yes |
| **Best For** | CRUD, queries | Most scenarios | Complex updates |

---

## 💡 1. Lightweight Session

The **fastest** and most **memory-efficient** session type.

### **Creating a Lightweight Session**

```csharp
await using var session = store.LightweightSession();
```

### **Characteristics**

**✅ Advantages:**
- Minimal overhead
- Best performance
- Low memory usage
- Great for read operations

**❌ Limitations:**
- No identity map (same document loaded multiple times = different instances)
- No automatic change tracking
- Must explicitly call `Store()` for updates

### **When to Use**

```csharp
// ✅ PERFECT - Simple CRUD
public static async Task LightweightUseCases(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // Create
    var user = new User { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com" };
    session.Store(user);

    // Read
    var loaded = await session.LoadAsync<User>(user.Id);

    // Update (must call Store again)
    var updated = loaded with { Name = "Alice Smith" };
    session.Store(updated);

    // Delete
    session.Delete<User>(user.Id);

    await session.SaveChangesAsync();
}

// ✅ PERFECT - Read-only queries
public static async Task<List<User>> GetAllUsers(IDocumentStore store)
{
    await using var session = store.LightweightSession();
    return await session.Query<User>().ToListAsync();
}

// ✅ PERFECT - Batch operations
public static async Task BatchInsert(IDocumentStore store, IEnumerable<User> users)
{
    await using var session = store.LightweightSession();
    foreach (var user in users)
    {
        session.Store(user);
    }
    await session.SaveChangesAsync();
}
```

### **No Identity Map Example**

```csharp
await using var session = store.LightweightSession();

var user1 = await session.LoadAsync<User>(userId);
var user2 = await session.LoadAsync<User>(userId); // Same ID

Console.WriteLine(ReferenceEquals(user1, user2)); // FALSE!
// Different instances even though same document
```

---

## 🗺️ 2. Identity Map Session (OpenSession)

Maintains an **identity map** to ensure one instance per document.

### **Creating an Identity Map Session**

```csharp
await using var session = store.OpenSession();
```

### **Characteristics**

**✅ Advantages:**
- Identity map ensures single instance per ID
- Consistent references within session
- Good for complex operations
- Prevents conflicting updates

**❌ Limitations:**
- Higher memory usage (caches documents)
- Still requires explicit `Store()` for updates
- No automatic change detection

### **Identity Map in Action**

```csharp
public static async Task IdentityMapDemo(IDocumentStore store, Guid userId)
{
    await using var session = store.OpenSession();

    var user1 = await session.LoadAsync<User>(userId);
    var user2 = await session.LoadAsync<User>(userId); // Same ID

    Console.WriteLine(ReferenceEquals(user1, user2)); // TRUE!
    // Same instance returned from cache

    // Both variables reference the same object
    user1.Name = "Changed"; // This doesn't actually work with records
    // But the concept is that you get the same instance
}
```

### **When to Use**

```csharp
// ✅ GOOD - Complex operations with multiple loads
public static async Task ComplexOperation(IDocumentStore store, Guid userId, Guid orderId)
{
    await using var session = store.OpenSession();

    // Load user multiple times in different parts of logic
    var user = await session.LoadAsync<User>(userId);

    // ... later in the code
    var sameUser = await session.LoadAsync<User>(userId); // Cached!

    // Ensures consistency within the session
}

// ✅ GOOD - Preventing conflicting updates
public static async Task PreventConflicts(IDocumentStore store, Guid userId)
{
    await using var session = store.OpenSession();

    var user = await session.LoadAsync<User>(userId);

    // Multiple operations on same document
    var updated1 = user with { Name = "New Name" };
    session.Store(updated1);

    // Loading again gets the stored version
    var reloaded = await session.LoadAsync<User>(userId);
    // reloaded reflects the pending Store() operation
}
```

---

## 🔍 3. Dirty Tracked Session

The most **intelligent** session - automatically tracks changes.

### **Creating a Dirty Tracked Session**

```csharp
await using var session = store.DirtyTrackedSession();
```

### **Characteristics**

**✅ Advantages:**
- Identity map included
- **Automatic change tracking**
- No need to call `Store()` for updates
- ORM-like experience

**❌ Limitations:**
- Highest memory usage
- Most overhead
- Only works with mutable objects (classes, not records)

### **Automatic Change Tracking**

```csharp
// Use a class, not a record
public class MutableUser
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public static async Task DirtyTrackingDemo(IDocumentStore store, Guid userId)
{
    await using var session = store.DirtyTrackedSession();

    var user = await session.LoadAsync<MutableUser>(userId);

    // Just modify the object
    user.Name = "Updated Name"; // No Store() call needed!
    user.Email = "new@example.com";

    // SaveChanges detects modifications automatically
    await session.SaveChangesAsync(); // Saves changes!
}
```

### **When to Use**

```csharp
// ✅ PERFECT - ORM-style workflows
public static async Task OrmStyleUpdate(IDocumentStore store, Guid userId)
{
    await using var session = store.DirtyTrackedSession();

    var user = await session.LoadAsync<MutableUser>(userId);

    if (user != null)
    {
        user.Name = "Modified";
        user.Email = "modified@example.com";
        // Automatically saved
    }

    await session.SaveChangesAsync();
}

// ✅ GOOD - Complex updates with multiple documents
public static async Task MultipleUpdates(IDocumentStore store)
{
    await using var session = store.DirtyTrackedSession();

    var users = await session.Query<MutableUser>()
        .Where(u => u.Email.EndsWith("@oldcompany.com"))
        .ToListAsync();

    foreach (var user in users)
    {
        user.Email = user.Email.Replace("@oldcompany.com", "@newcompany.com");
        // No Store() needed!
    }

    await session.SaveChangesAsync(); // All changes saved
}
```

**⚠️ Important:** Dirty tracking only works with **mutable classes**, not **immutable records**!

```csharp
// ❌ WON'T WORK - Records are immutable
await using var session = store.DirtyTrackedSession();
var user = await session.LoadAsync<User>(userId); // User is a record
user = user with { Name = "Changed" }; // Creates new instance!
await session.SaveChangesAsync(); // Nothing saved! Old instance tracked

// ✅ WORKS - Explicit Store with records
session.Store(user with { Name = "Changed" });
await session.SaveChangesAsync();
```

---

## 🔄 Transaction Management

Sessions wrap operations in PostgreSQL transactions.

### **Basic Transaction**

```csharp
await using var session = store.LightweightSession();

session.Store(user1);
session.Store(user2);
session.Delete<Product>(productId);

// All or nothing - atomic transaction
await session.SaveChangesAsync();
```

### **Transaction Rollback**

```csharp
public static async Task TransactionRollback(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    try
    {
        session.Store(user1);
        session.Store(user2);

        // Something fails
        throw new InvalidOperationException("Oops!");

        await session.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        // Transaction automatically rolled back
        // user1 and user2 are NOT saved
        Console.WriteLine($"Transaction rolled back: {ex.Message}");
    }
}
```

### **Multiple SaveChanges Calls**

```csharp
await using var session = store.LightweightSession();

// First transaction
session.Store(user1);
await session.SaveChangesAsync(); // Committed

// Second transaction
session.Store(user2);
await session.SaveChangesAsync(); // Committed

// Each SaveChangesAsync is a separate transaction
```

### **Explicit Transaction Control**

```csharp
// Advanced: Use Npgsql transaction directly
await using var session = store.LightweightSession();

var transaction = session.Connection.BeginTransaction();

try
{
    session.Store(user);
    await session.SaveChangesAsync();

    // Other database operations
    await DoCustomSqlAsync(session.Connection);

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

---

## ⏱️ Session Lifetime Management

### **Short-Lived Sessions (Recommended)**

```csharp
// ✅ BEST PRACTICE - Create session per operation
public async Task<User?> GetUserByEmail(string email)
{
    await using var session = _store.LightweightSession();
    return await session.Query<User>()
        .FirstOrDefaultAsync(u => u.Email == email);
}

public async Task UpdateUser(User user)
{
    await using var session = _store.LightweightSession();
    session.Store(user);
    await session.SaveChangesAsync();
}
```

**Benefits:**
- ✅ No memory leaks
- ✅ Fresh data every operation
- ✅ Simple lifetime management

### **Session Per Request (Web APIs)**

```csharp
// ASP.NET Core - Scoped session
public void ConfigureServices(IServiceCollection services)
{
    services.AddMarten(opts =>
    {
        opts.Connection(connectionString);
    })
    .UseLightweightSessions(); // Scoped session per HTTP request

    // Or
    services.AddScoped(sp =>
    {
        var store = sp.GetRequiredService<IDocumentStore>();
        return store.LightweightSession();
    });
}

// Controller
public class UsersController : ControllerBase
{
    private readonly IDocumentSession _session;

    public UsersController(IDocumentSession session)
    {
        _session = session; // Injected, disposed automatically
    }

    [HttpGet("{id}")]
    public async Task<User?> GetUser(Guid id)
    {
        return await _session.LoadAsync<User>(id);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(User user)
    {
        _session.Store(user);
        await _session.SaveChangesAsync();
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }
}
```

### **Long-Lived Sessions (Anti-Pattern)**

```csharp
// ❌ BAD - Don't do this
public class UserService
{
    private readonly IDocumentSession _session;

    public UserService(IDocumentStore store)
    {
        _session = store.LightweightSession(); // Lives forever!
    }

    public async Task<User?> GetUser(Guid id)
    {
        return await _session.LoadAsync<User>(id); // Stale data!
    }
}
```

**Problems:**
- ❌ Memory leaks (identity map grows)
- ❌ Stale data
- ❌ Connection held open
- ❌ Concurrency issues

---

## 💡 Choosing the Right Session Type

### **Decision Tree**

```
Are you using immutable records (recommended)?
├─ YES → Use LightweightSession (fastest)
│   └─ Need identity map? → Use OpenSession
└─ NO (using mutable classes)
    └─ Need automatic change tracking? → Use DirtyTrackedSession
```

### **Common Scenarios**

```csharp
// ✅ Read-only queries
public async Task<List<User>> GetUsers(IDocumentStore store)
{
    await using var session = store.LightweightSession(); // Fastest
    return await session.Query<User>().ToListAsync();
}

// ✅ Simple CRUD with records
public async Task CreateUser(IDocumentStore store, User user)
{
    await using var session = store.LightweightSession();
    session.Store(user);
    await session.SaveChangesAsync();
}

// ✅ Complex operation with multiple loads
public async Task ComplexLogic(IDocumentStore store, Guid userId)
{
    await using var session = store.OpenSession(); // Identity map
    // ... logic that loads same document multiple times
}

// ✅ ORM-style with mutable classes
public async Task OrmUpdate(IDocumentStore store, Guid userId)
{
    await using var session = store.DirtyTrackedSession();
    var user = await session.LoadAsync<MutableUser>(userId);
    user.Name = "Updated"; // Automatic tracking
    await session.SaveChangesAsync();
}
```

---

## 🎓 Hands-On Exercises

### **Exercise 1: Compare Session Types**

```csharp
public static async Task CompareSessionTypes(IDocumentStore store, Guid userId)
{
    // Lightweight - No identity map
    await using (var session = store.LightweightSession())
    {
        var user1 = await session.LoadAsync<User>(userId);
        var user2 = await session.LoadAsync<User>(userId);
        Console.WriteLine($"Lightweight - Same instance: {ReferenceEquals(user1, user2)}");
    }

    // OpenSession - Identity map
    await using (var session = store.OpenSession())
    {
        var user1 = await session.LoadAsync<User>(userId);
        var user2 = await session.LoadAsync<User>(userId);
        Console.WriteLine($"OpenSession - Same instance: {ReferenceEquals(user1, user2)}");
    }
}
```

### **Exercise 2: Transaction Management**

```csharp
public static async Task TransactionExample(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    try
    {
        var user1 = new User { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com" };
        var user2 = new User { Id = Guid.NewGuid(), Name = "Bob", Email = "bob@example.com" };

        session.Store(user1);
        session.Store(user2);

        // Simulate failure
        if (DateTime.Now.Second % 2 == 0)
        {
            throw new Exception("Simulated failure");
        }

        await session.SaveChangesAsync();
        Console.WriteLine("Transaction committed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Transaction rolled back: {ex.Message}");
    }
}
```

### **Exercise 3: Batch Update Pattern**

```csharp
public static async Task BatchUpdate(IDocumentStore store, string oldDomain, string newDomain)
{
    await using var session = store.LightweightSession();

    var users = await session.Query<User>()
        .Where(u => u.Email.EndsWith(oldDomain))
        .ToListAsync();

    Console.WriteLine($"Updating {users.Count} users");

    foreach (var user in users)
    {
        var updated = user with { Email = user.Email.Replace(oldDomain, newDomain) };
        session.Store(updated);
    }

    await session.SaveChangesAsync();
    Console.WriteLine("Batch update complete");
}
```

---

## 💡 Best Practices

### **1. Always Dispose Sessions**

```csharp
// ✅ GOOD - using/await using
await using var session = store.LightweightSession();
// Automatically disposed

// ❌ BAD - Manual dispose
var session = store.LightweightSession();
try { /* ... */ }
finally { await session.DisposeAsync(); }
```

### **2. Keep Sessions Short-Lived**

```csharp
// ✅ GOOD - Per operation
public async Task<User> GetUser(Guid id)
{
    await using var session = _store.LightweightSession();
    return await session.LoadAsync<User>(id);
}

// ❌ BAD - Long-lived
private readonly IDocumentSession _session = store.LightweightSession();
```

### **3. Batch Operations**

```csharp
// ✅ GOOD - Single session
await using var session = store.LightweightSession();
foreach (var user in users) { session.Store(user); }
await session.SaveChangesAsync();

// ❌ BAD - Session per item
foreach (var user in users)
{
    await using var session = store.LightweightSession();
    session.Store(user);
    await session.SaveChangesAsync();
}
```

### **4. Use Lightweight by Default**

```csharp
// ✅ Default to Lightweight
await using var session = store.LightweightSession();

// Only use others when needed
// await using var session = store.OpenSession(); // Need identity map?
// await using var session = store.DirtyTrackedSession(); // Need change tracking?
```

---

## 📊 Performance Comparison

```
Operation: Load document 1000 times

Lightweight:     100ms  ⚡⚡⚡
OpenSession:     150ms  ⚡⚡
DirtyTracked:    200ms  ⚡

Memory Usage:

Lightweight:     10MB   💾
OpenSession:     25MB   💾💾
DirtyTracked:    40MB   💾💾💾
```

---

## 🎓 What You've Learned

Excellent! You now understand:

- ✅ **Three session types** - Lightweight, OpenSession, DirtyTracked
- ✅ **When to use each** - Performance vs. features trade-offs
- ✅ **Transaction management** - Atomic operations, rollbacks
- ✅ **Session lifetimes** - Short-lived vs. long-lived
- ✅ **Best practices** - Dispose, batch, default to lightweight

---

## 🚀 Next Steps

Ready for concurrency? In **[Chapter 05 - Concurrency](TUTORIAL-05-Concurrency.md)**, we'll explore:

- Optimistic concurrency control
- Version tracking
- Conflict detection and resolution
- Preventing lost updates

**Continue to [Chapter 05 - Concurrency](TUTORIAL-05-Concurrency.md) →**

---

**Questions?** Review this chapter or go back to [Chapter 03 - Identity & Schema](TUTORIAL-03-Identity-Schema.md).
