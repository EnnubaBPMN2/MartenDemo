# 📖 Chapter 01 - Document Database Basics

Welcome to your first hands-on chapter! Here we'll explore Marten's document database fundamentals using the code already in your repository.

---

## 🎯 Chapter Objectives

By the end of this chapter, you will understand:

- ✅ What a document database is and how Marten implements it
- ✅ How to configure and create a DocumentStore
- ✅ The role of sessions in Marten
- ✅ Basic CRUD operations (Create, Read, Update, Delete)
- ✅ What PostgreSQL tables and structures Marten creates

---

## 📚 What is a Document Database?

### **Traditional Relational Approach:**

```sql
-- Multiple tables with relationships
CREATE TABLE users (
    id UUID PRIMARY KEY,
    name VARCHAR(100),
    email VARCHAR(255)
);

CREATE TABLE addresses (
    id UUID PRIMARY KEY,
    user_id UUID REFERENCES users(id),
    street VARCHAR(255),
    city VARCHAR(100)
);
```

### **Document Database Approach:**

```csharp
// Single document with embedded data
public record User
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Email { get; init; }
    public Address Address { get; init; } // Embedded!
}
```

**Key Difference:**
- **Relational**: Data spread across tables, joined via foreign keys
- **Document**: Self-contained JSON documents, no joins needed

**Marten's Approach:**
- Store .NET objects as JSONB in PostgreSQL
- Query using LINQ (translated to SQL)
- Get performance of PostgreSQL + flexibility of documents

---

## 🏗️ Core Concepts

### **1. DocumentStore**

The `DocumentStore` is the entry point to Marten. Think of it as:
- **Connection Manager**: Handles database connections
- **Configuration Hub**: Defines how documents are stored
- **Session Factory**: Creates sessions for operations

**Key Points:**
- Create once per application (singleton pattern)
- Thread-safe and reusable
- Expensive to create, so cache it

### **2. Session**

A `Session` represents a **unit of work** - a batch of operations:
- Track changes to documents
- Execute queries
- Commit changes as a transaction

**Analogy**: Like a shopping cart
- Add/remove items (Store/Delete documents)
- Browse products (Query documents)
- Checkout (SaveChangesAsync commits everything)

### **3. Document**

A **document** is any .NET object that Marten can serialize to JSON:
- Usually POCOs (Plain Old CLR Objects)
- Must have an `Id` property (Guid, int, long, or string)
- Can be classes or records

---

## 💻 Code Walkthrough

Let's examine your existing `Program.cs` step by step.

### **Step 1: Document Definition**

```csharp
public record User
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
}
```

**Analysis:**
- ✅ Uses `record` - immutable by default (good for documents)
- ✅ `Guid Id` - Marten recognizes this as the document identifier
- ✅ `required` properties - ensures valid data
- ✅ `init` accessors - immutable after creation

**Marten Requirements:**
- Must have a property named `Id`, `id`, or decorated with `[Identity]`
- Supported ID types: `Guid`, `int`, `long`, `string`

### **Step 2: Configuration Loading**

```csharp
// Program.cs:19-23
var builder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", false, true)
    .AddEnvironmentVariables();

var config = builder.Build();
```

**What's happening:**
- Load configuration from `appsettings.json`
- Allow environment variables to override settings
- Standard .NET configuration pattern

### **Step 3: Connection String Resolution**

```csharp
// Program.cs:26-32
var connection = Environment.GetEnvironmentVariable("CONN")
                 ?? config.GetConnectionString("Postgres");

if (string.IsNullOrWhiteSpace(connection))
{
    throw new InvalidOperationException("No PostgreSQL connection string found.");
}
```

**Configuration Priority:**
1. Environment variable `CONN` (highest priority)
2. `appsettings.json` → `ConnectionStrings:Postgres`
3. Exception if neither exists

**💡 Best Practice:**
- Use environment variables for production (security)
- Use appsettings.json for local development (convenience)

### **Step 4: DocumentStore Creation**

```csharp
// Program.cs:36-43
using var store = DocumentStore.For(opts =>
{
    opts.Connection(connection);
    opts.AutoCreateSchemaObjects = Enum.TryParse<AutoCreate>(
        config["Marten:AutoCreateSchemaObjects"], out var autoCreate)
        ? autoCreate
        : AutoCreate.None;
});
```

**Breaking it down:**

**`DocumentStore.For()`** - Static factory method
- Creates and configures the store
- Returns `IDocumentStore` instance

**`opts.Connection(connection)`** - Set PostgreSQL connection
- Required configuration
- Uses standard Npgsql connection string

**`opts.AutoCreateSchemaObjects`** - Schema management
- `AutoCreate.All` - Create tables/indexes automatically (development)
- `AutoCreate.CreateOrUpdate` - Update schema if changed
- `AutoCreate.CreateOnly` - Create if missing, don't update
- `AutoCreate.None` - Manual schema management (production)

**`using var store`** - Dispose pattern
- Store implements `IDisposable`
- Cleans up resources when done

**💡 Development vs. Production:**
```csharp
// Development
opts.AutoCreateSchemaObjects = AutoCreate.All;

// Production
opts.AutoCreateSchemaObjects = AutoCreate.None;
// Use migrations instead (covered in Chapter 08)
```

### **Step 5: Creating a Session**

```csharp
// Program.cs:46
await using var session = store.LightweightSession();
```

**Session Types:**
- **LightweightSession()** - No identity map, no change tracking (fastest)
- **OpenSession()** - Identity map enabled (covered in Chapter 04)
- **DirtyTrackedSession()** - Full change tracking (covered in Chapter 04)

**For now, use LightweightSession:**
- Best performance
- Perfect for simple CRUD
- Good for most scenarios

**`await using`** - Async dispose pattern
- Sessions implement `IAsyncDisposable`
- Ensures proper cleanup

### **Step 6: Storing a Document**

```csharp
// Program.cs:48-50
var user = new User { Id = Guid.NewGuid(), Name = "Hermann", Email = "h@ennuba.com" };
session.Store(user);
await session.SaveChangesAsync();
```

**What happens:**

1. **Create document instance**
   ```csharp
   var user = new User { Id = Guid.NewGuid(), ... };
   ```
   - Generate unique ID
   - Create immutable record

2. **Stage for storage**
   ```csharp
   session.Store(user);
   ```
   - Adds document to session's pending operations
   - **Nothing is written to database yet!**
   - Think: Adding item to shopping cart

3. **Commit to database**
   ```csharp
   await session.SaveChangesAsync();
   ```
   - Executes all pending operations
   - Wrapped in a PostgreSQL transaction
   - Think: Checkout - paying for all items at once

**⚠️ Common Mistake:**
```csharp
// This does nothing - forgot to SaveChanges!
var user = new User { Id = Guid.NewGuid(), Name = "Test", Email = "test@test.com" };
session.Store(user);
// user is NOT in the database yet
```

### **Step 7: Querying Documents**

```csharp
// Program.cs:53-54
var dbUser = session.Query<User>().FirstOrDefault(u => u.Email == "h@ennuba.com");
Console.WriteLine($"Usuario guardado: {dbUser?.Name} - {dbUser?.Email}");
```

**Query Anatomy:**

**`session.Query<User>()`** - Start a LINQ query
- Returns `IQueryable<User>`
- Queries the `User` document table

**`.FirstOrDefault(u => u.Email == "h@ennuba.com")`** - LINQ filter
- Marten translates to PostgreSQL query
- Returns matching document or `null`

**LINQ Translation:**
```csharp
// C# LINQ
session.Query<User>().FirstOrDefault(u => u.Email == "h@ennuba.com")

// Becomes PostgreSQL
SELECT data FROM public.mt_doc_user
WHERE data->>'Email' = 'h@ennuba.com'
LIMIT 1;
```

---

## 🔍 PostgreSQL Deep Dive

Let's see what Marten creates in your database!

### **Table Structure**

When you run the program, Marten creates:

```sql
-- Main document table
CREATE TABLE public.mt_doc_user (
    id           UUID PRIMARY KEY,
    data         JSONB NOT NULL,
    mt_last_modified TIMESTAMP DEFAULT now(),
    mt_version   UUID NOT NULL DEFAULT gen_random_uuid(),
    mt_dotnet_type VARCHAR
);
```

**Column Breakdown:**

| Column | Purpose |
|--------|---------|
| `id` | Document identifier (from `User.Id`) |
| `data` | Full document as JSONB |
| `mt_last_modified` | Automatic timestamp tracking |
| `mt_version` | Optimistic concurrency (Chapter 05) |
| `mt_dotnet_type` | Supports inheritance (Chapter 08) |

### **Viewing Your Data**

Connect to PostgreSQL and explore:

```sql
-- See all users
SELECT * FROM public.mt_doc_user;

-- View the JSON structure
SELECT
    id,
    data->>'Name' as name,
    data->>'Email' as email,
    mt_last_modified
FROM public.mt_doc_user;

-- See raw JSONB
SELECT data FROM public.mt_doc_user;
```

**Example Output:**
```json
{
  "Id": "a1b2c3d4-...",
  "Name": "Hermann",
  "Email": "h@ennuba.com"
}
```

### **Indexes**

Marten creates basic indexes:

```sql
-- Primary key index (automatic)
CREATE UNIQUE INDEX pk_mt_doc_user ON public.mt_doc_user(id);

-- Last modified index
CREATE INDEX mt_doc_user_mt_last_modified ON public.mt_doc_user(mt_last_modified);
```

**💡 Performance Tip:**
- JSONB columns support GIN indexes
- Add custom indexes for frequently queried fields (Chapter 03)

---

## 🚀 Hands-On Exercises

Let's enhance the basic example with more operations!

### **Exercise 1: Complete CRUD Operations**

Add these methods to `Program.cs`:

```csharp
public static async Task BasicCrudExample(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // CREATE
    var user = new User
    {
        Id = Guid.NewGuid(),
        Name = "Jane Doe",
        Email = "jane@example.com"
    };
    session.Store(user);
    await session.SaveChangesAsync();
    Console.WriteLine($"✅ Created user: {user.Name}");

    // READ
    var loadedUser = await session.LoadAsync<User>(user.Id);
    Console.WriteLine($"📖 Loaded user: {loadedUser?.Name}");

    // UPDATE (with new document)
    var updatedUser = user with { Name = "Jane Smith" };
    session.Store(updatedUser);
    await session.SaveChangesAsync();
    Console.WriteLine($"🔄 Updated user: {updatedUser.Name}");

    // DELETE
    session.Delete<User>(user.Id);
    await session.SaveChangesAsync();
    Console.WriteLine($"🗑️ Deleted user: {user.Id}");

    // VERIFY DELETION
    var deletedUser = await session.LoadAsync<User>(user.Id);
    Console.WriteLine($"Deleted user exists: {deletedUser != null}"); // False
}
```

**Key Points:**
- **LoadAsync()**: Load by ID (faster than Query)
- **Records are immutable**: Use `with` to create modified copies
- **Delete()**: Removes document from database

### **Exercise 2: Batch Operations**

Marten excels at batch operations:

```csharp
public static async Task BatchOperations(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // Create multiple users in one transaction
    var users = new[]
    {
        new User { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com" },
        new User { Id = Guid.NewGuid(), Name = "Bob", Email = "bob@example.com" },
        new User { Id = Guid.NewGuid(), Name = "Charlie", Email = "charlie@example.com" }
    };

    session.Store(users); // Store array
    await session.SaveChangesAsync(); // Single transaction

    Console.WriteLine($"✅ Created {users.Length} users in one transaction");

    // Query all users
    var allUsers = await session.Query<User>().ToListAsync();
    Console.WriteLine($"📊 Total users: {allUsers.Count}");
}
```

**Benefits:**
- ✅ Single database roundtrip
- ✅ Atomic transaction (all or nothing)
- ✅ Better performance

### **Exercise 3: Query Patterns**

Different ways to retrieve documents:

```csharp
public static async Task QueryPatterns(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // 1. Load by ID (fastest - primary key lookup)
    var byId = await session.LoadAsync<User>(someGuid);

    // 2. Load multiple by IDs
    var ids = new[] { guid1, guid2, guid3 };
    var byIds = await session.LoadManyAsync<User>(ids);

    // 3. Simple LINQ query
    var byEmail = await session.Query<User>()
        .FirstOrDefaultAsync(u => u.Email == "test@example.com");

    // 4. Multiple conditions
    var filtered = await session.Query<User>()
        .Where(u => u.Name.Contains("Smith") && u.Email.EndsWith("@example.com"))
        .ToListAsync();

    // 5. Ordering and limiting
    var recent = await session.Query<User>()
        .OrderByDescending(u => u.Id)
        .Take(10)
        .ToListAsync();
}
```

---

## 💡 Best Practices

### **1. Store is Expensive, Session is Cheap**

```csharp
// ✅ GOOD - Create store once
public class UserRepository
{
    private readonly IDocumentStore _store;

    public UserRepository(IDocumentStore store)
    {
        _store = store; // Injected singleton
    }

    public async Task<User> GetUserAsync(Guid id)
    {
        await using var session = _store.LightweightSession(); // Create per operation
        return await session.LoadAsync<User>(id);
    }
}

// ❌ BAD - Don't recreate store
public async Task BadExample()
{
    using var store = DocumentStore.For("connection"); // Expensive!
    // ... use store
}
```

### **2. Always Dispose Sessions**

```csharp
// ✅ GOOD - using/await using
await using var session = store.LightweightSession();
// Session automatically disposed

// ❌ BAD - Manual management
var session = store.LightweightSession();
// Do stuff
// Forgot to dispose - connection leak!
```

### **3. Batch Operations**

```csharp
// ✅ GOOD - Single transaction
await using var session = store.LightweightSession();
foreach (var user in users)
{
    session.Store(user);
}
await session.SaveChangesAsync(); // Once

// ❌ BAD - Multiple transactions
foreach (var user in users)
{
    await using var session = store.LightweightSession();
    session.Store(user);
    await session.SaveChangesAsync(); // Many times - slow!
}
```

### **4. Use LoadAsync for Known IDs**

```csharp
// ✅ GOOD - Direct ID lookup
var user = await session.LoadAsync<User>(userId);

// ❌ LESS EFFICIENT - Query when ID is known
var user = await session.Query<User>()
    .FirstOrDefaultAsync(u => u.Id == userId);
```

---

## ⚠️ Common Pitfalls

### **1. Forgetting SaveChangesAsync**

```csharp
// ❌ WRONG - Changes never saved
session.Store(user);
// Forgot SaveChangesAsync - user not in database!

// ✅ CORRECT
session.Store(user);
await session.SaveChangesAsync();
```

### **2. Reusing IDs Incorrectly**

```csharp
// ❌ WRONG - Same ID overwrites
var user1 = new User { Id = someGuid, Name = "First" };
var user2 = new User { Id = someGuid, Name = "Second" }; // Same ID!

session.Store(user1);
session.Store(user2);
await session.SaveChangesAsync();
// Only "Second" exists - user1 was overwritten

// ✅ CORRECT - Unique IDs
var user1 = new User { Id = Guid.NewGuid(), Name = "First" };
var user2 = new User { Id = Guid.NewGuid(), Name = "Second" };
```

### **3. Session Lifespan**

```csharp
// ❌ WRONG - Session lives too long
var session = store.LightweightSession();
while (true)
{
    var user = GetUserFromSomewhere();
    session.Store(user);
    await session.SaveChangesAsync();
    // Memory grows, connection held open
}

// ✅ CORRECT - Short-lived sessions
while (true)
{
    var user = GetUserFromSomewhere();
    await using var session = store.LightweightSession();
    session.Store(user);
    await session.SaveChangesAsync();
} // Session disposed each iteration
```

---

## 📊 Performance Considerations

### **JSONB vs. Relational**

**When Marten (JSONB) Shines:**
- ✅ Self-contained documents
- ✅ Flexible schema
- ✅ No joins needed
- ✅ Rapid development

**When Traditional Relational is Better:**
- ❌ Heavy joins across many tables
- ❌ Strict referential integrity
- ❌ Complex aggregations
- ❌ Legacy systems

### **Indexing Preview**

Queries on JSONB fields can be slow without indexes:

```csharp
// Slow without index
var user = session.Query<User>()
    .FirstOrDefault(u => u.Email == "test@example.com");
// Full table scan!

// Chapter 03 covers how to add indexes:
// opts.Schema.For<User>().Index(x => x.Email);
```

---

## 🎓 What You've Learned

Congratulations! You now understand:

- ✅ **Document databases** - Self-contained JSON documents vs. relational tables
- ✅ **DocumentStore** - Central configuration and session factory
- ✅ **Sessions** - Unit of work pattern for operations
- ✅ **CRUD basics** - Store, Load, Query, Delete
- ✅ **PostgreSQL internals** - How Marten uses JSONB tables
- ✅ **Best practices** - Singleton store, short-lived sessions, batching

---

## 🚀 Next Steps

Ready to level up? In **[Chapter 02 - Querying](TUTORIAL-02-Querying.md)**, we'll explore:

- Advanced LINQ queries
- Compiled queries for performance
- Full-text search
- Aggregations and grouping
- Complex filtering

**Before moving on:**
- [ ] Run the basic example and verify it works
- [ ] Connect to PostgreSQL and explore the `mt_doc_user` table
- [ ] Try the exercises above
- [ ] Experiment with your own document types

**Continue to [Chapter 02 - Querying](TUTORIAL-02-Querying.md) →**

---

**Questions or stuck?** Review this chapter or revisit the [Introduction](TUTORIAL-00-Introduction.md).
