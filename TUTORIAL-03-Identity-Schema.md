# 🏗️ Chapter 03 - Identity & Schema Management

Understanding how Marten identifies and organizes your documents is crucial for building robust applications. This chapter covers ID strategies, indexes, constraints, and schema customization.

---

## 🎯 Chapter Objectives

By the end of this chapter, you will:

- ✅ Understand ID generation strategies
- ✅ Implement custom identity patterns
- ✅ Create indexes for query performance
- ✅ Add unique constraints
- ✅ Customize document schema
- ✅ Work with soft deletes
- ✅ Understand PostgreSQL schema creation

---

## 🆔 Identity Strategies

### **Default Identity: Property Named "Id"**

Marten automatically recognizes these patterns:

```csharp
// Option 1: Property named "Id" (any case)
public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}

// Option 2: Lowercase "id"
public class Product
{
    public Guid id { get; set; }
    public string Name { get; set; }
}

// Option 3: Custom property with [Identity] attribute
public class Order
{
    [Identity]
    public Guid OrderNumber { get; set; }
    public decimal Total { get; set; }
}
```

### **Supported ID Types**

```csharp
// 1. Guid (recommended)
public record User
{
    public Guid Id { get; init; }
}

// 2. int (auto-incrementing)
public record Counter
{
    public int Id { get; init; }
}

// 3. long (auto-incrementing)
public record BigCounter
{
    public long Id { get; init; }
}

// 4. string
public record CustomId
{
    public string Id { get; init; } = "";
}
```

### **ID Generation Strategies**

#### **1. Client-Generated GUIDs (Default)**

```csharp
// You create the ID
var user = new User
{
    Id = Guid.NewGuid(), // Client generates
    Name = "Hermann"
};

session.Store(user);
await session.SaveChangesAsync();
```

**Advantages:**
- ✅ No database roundtrip needed
- ✅ Know ID before saving
- ✅ Works in distributed systems
- ✅ Good for batch operations

**Disadvantages:**
- ❌ Not sequential (index fragmentation)
- ❌ Larger than integers (16 bytes)

#### **2. Server-Generated GUIDs**

```csharp
// Configure Marten to generate
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    opts.Schema.For<User>()
        .IdStrategy(new GuidIdGeneration()); // Server generates
});

// Don't set ID - Marten will assign it
var user = new User { Name = "Hermann" };
session.Store(user);
await session.SaveChangesAsync();

Console.WriteLine($"Generated ID: {user.Id}");
```

#### **3. Sequential GUIDs (Hi-Lo)**

Best of both worlds - client-generated but sequential:

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    opts.Schema.For<User>()
        .IdStrategy(new HiLoIdGeneration(opts.Schema.For<User>(), new HiloSettings
        {
            MaxLo = 1000 // Fetch 1000 IDs at a time
        }));
});

// Generates sequential GUIDs
var user = new User { Name = "Hermann" };
session.Store(user);
await session.SaveChangesAsync();
```

**How Hi-Lo Works:**
1. Fetch a range of IDs from database (e.g., 1000)
2. Generate IDs locally from that range
3. When exhausted, fetch next range
4. Results in sequential IDs with minimal DB calls

#### **4. Auto-Incrementing Integers**

```csharp
public record User
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}

var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    opts.Schema.For<User>()
        .IdStrategy(new IntIdGeneration()); // PostgreSQL sequence
});

// Don't set ID
var user = new User { Name = "Hermann" };
session.Store(user);
await session.SaveChangesAsync();

Console.WriteLine($"Assigned ID: {user.Id}"); // 1, 2, 3...
```

**PostgreSQL Implementation:**
```sql
CREATE SEQUENCE mt_doc_user_id_seq;

ALTER TABLE mt_doc_user
    ALTER COLUMN id SET DEFAULT nextval('mt_doc_user_id_seq');
```

#### **5. Custom ID Generation**

```csharp
// Custom format: USER-{GUID}
public class UserIdGenerator : IIdGeneration
{
    public IEnumerable<Type> KeyTypes => new[] { typeof(string) };

    public object Assign(ITenant tenant, Type documentType, object document, IMartenSession session)
    {
        var id = $"USER-{Guid.NewGuid():N}";
        if (document is User user)
        {
            return user with { Id = id };
        }
        return document;
    }

    public string ToJson(object id) => id.ToString()!;
}

// Configuration
opts.Schema.For<User>()
    .IdStrategy(new UserIdGenerator());
```

---

## 🔍 Indexes

Indexes dramatically improve query performance for WHERE and ORDER BY clauses.

### **Creating Indexes**

#### **1. Simple Property Index**

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Index on Email property
    opts.Schema.For<User>()
        .Index(x => x.Email);
});
```

**Generated SQL:**
```sql
CREATE INDEX mt_doc_user_idx_email
    ON mt_doc_user USING btree ((data->>'Email'));
```

#### **2. Multiple Property Indexes**

```csharp
// Separate indexes
opts.Schema.For<User>()
    .Index(x => x.Name)
    .Index(x => x.Email);

// Composite index (for queries using both)
opts.Schema.For<User>()
    .Index(x => x.Name, x => x.Email);
```

#### **3. Nested Property Index**

```csharp
public record Address(string City, string State, string ZipCode);

public record Contact
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public Address HomeAddress { get; init; }
}

// Index nested property
opts.Schema.For<Contact>()
    .Index(x => x.HomeAddress.City);
```

**Generated SQL:**
```sql
CREATE INDEX mt_doc_contact_idx_city
    ON mt_doc_contact USING btree ((data->'HomeAddress'->>'City'));
```

#### **4. GIN Index for JSONB**

For complex queries on entire documents:

```csharp
opts.Schema.For<User>()
    .GinIndexJsonData(); // Full document GIN index
```

**When to Use:**
- Complex queries on multiple fields
- Full-text search
- Array/collection queries
- General-purpose indexing

**Trade-offs:**
- ✅ Flexible - supports many query types
- ❌ Larger index size
- ❌ Slower writes

#### **5. Full-Text Search Index**

```csharp
opts.Schema.For<User>()
    .FullTextIndex(x => x.Name); // Single property

opts.Schema.For<User>()
    .FullTextIndex(x => x.Name, x => x.Email); // Multiple properties

// Custom configuration
opts.Schema.For<User>()
    .FullTextIndex("search_idx", x => x.Name)
    .RegConfig("english") // Language configuration
    .Weight(TextSearchWeight.A); // Priority weight
```

---

## 🔒 Unique Constraints

Ensure field uniqueness at the database level.

### **Unique Index**

```csharp
// Email must be unique
opts.Schema.For<User>()
    .UniqueIndex(x => x.Email);
```

**Generated SQL:**
```sql
CREATE UNIQUE INDEX mt_doc_user_uidx_email
    ON mt_doc_user USING btree ((data->>'Email'));
```

**Behavior:**
```csharp
// First user - succeeds
var user1 = new User { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com" };
session.Store(user1);
await session.SaveChangesAsync(); // OK

// Second user with same email - fails
var user2 = new User { Id = Guid.NewGuid(), Name = "Bob", Email = "alice@example.com" };
session.Store(user2);
await session.SaveChangesAsync(); // Throws PostgresException
```

### **Handling Unique Constraint Violations**

```csharp
public static async Task<bool> CreateUserWithUniqueEmail(
    IDocumentStore store, User user)
{
    try
    {
        await using var session = store.LightweightSession();
        session.Store(user);
        await session.SaveChangesAsync();
        return true;
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505") // Unique violation
    {
        Console.WriteLine($"Email {user.Email} already exists");
        return false;
    }
}
```

### **Composite Unique Constraints**

```csharp
public record Enrollment
{
    public Guid Id { get; init; }
    public Guid StudentId { get; init; }
    public Guid CourseId { get; init; }
}

// Student can only enroll in each course once
opts.Schema.For<Enrollment>()
    .UniqueIndex(x => x.StudentId, x => x.CourseId);
```

---

## 🎨 Schema Customization

### **Custom Table Names**

```csharp
// Default: mt_doc_user
// Custom: app_users
opts.Schema.For<User>()
    .DocumentAlias("app_users");
```

### **Custom Schema (PostgreSQL Schema)**

```csharp
// Default: public.mt_doc_user
// Custom: app.users
opts.DatabaseSchemaName = "app";

opts.Schema.For<User>()
    .DatabaseSchemaName("app");
```

**Generated:**
```sql
CREATE SCHEMA IF NOT EXISTS app;
CREATE TABLE app.mt_doc_user (...);
```

### **Exclude Properties from Storage**

```csharp
public record User
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";

    [JsonIgnore] // Not stored in database
    public string FullName => $"{FirstName} {LastName}";
}
```

### **Computed Indexes**

Index on computed values:

```csharp
public record User
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
}

opts.Schema.For<User>()
    .Index(x => x.FirstName + " " + x.LastName);
```

---

## 🗑️ Soft Deletes

Keep deleted documents in the database with a flag.

### **Enable Soft Deletes**

```csharp
opts.Schema.For<User>()
    .SoftDeleted(); // Adds mt_deleted boolean column
```

**Generated Schema:**
```sql
ALTER TABLE mt_doc_user
    ADD COLUMN mt_deleted BOOLEAN DEFAULT FALSE;

CREATE INDEX mt_doc_user_idx_deleted
    ON mt_doc_user(mt_deleted);
```

### **Using Soft Deletes**

```csharp
public static async Task SoftDeleteExample(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    var user = new User { Id = Guid.NewGuid(), Name = "Test", Email = "test@example.com" };
    session.Store(user);
    await session.SaveChangesAsync();

    // Soft delete - marks mt_deleted = true
    session.Delete(user);
    await session.SaveChangesAsync();

    // Regular query doesn't return deleted
    var notFound = await session.LoadAsync<User>(user.Id); // null

    // Query for deleted documents
    var deleted = await session.Query<User>()
        .Where(x => x.IsDeleted())
        .ToListAsync();

    // Include deleted in queries
    var includingDeleted = await session.Query<User>()
        .Where(x => x.MaybeDeleted())
        .ToListAsync();
}
```

### **Restore Deleted Documents**

```csharp
// Hard delete (remove from database)
session.HardDelete(user);
await session.SaveChangesAsync();

// Or restore by re-storing
var restoredUser = user with { /* any changes */ };
session.Store(restoredUser);
await session.SaveChangesAsync();
```

---

## 📐 Schema Generation Strategies

### **Development: Auto-Create**

```csharp
opts.AutoCreateSchemaObjects = AutoCreate.All;
```

**What it does:**
- Creates tables, indexes, sequences
- Updates schema when document changes
- Drops and recreates if needed

**⚠️ WARNING:** Can cause data loss in production!

### **Production: Manual Migrations**

```csharp
opts.AutoCreateSchemaObjects = AutoCreate.None;
```

**Generate migration scripts:**

```csharp
// Export schema to SQL file
await store.Schema.ApplyAllConfiguredChangesToDatabaseAsync();

// Or generate script
var writer = new StringWriter();
store.Schema.WriteDDL(writer);
var ddl = writer.ToString();
File.WriteAllText("migrations/001_initial.sql", ddl);
```

**Apply manually:**
```bash
psql -U postgres -d marten_demo -f migrations/001_initial.sql
```

### **Staging: Create if Missing**

```csharp
opts.AutoCreateSchemaObjects = AutoCreate.CreateOnly;
```

Creates schema objects if they don't exist, but won't update existing ones.

---

## 🔍 PostgreSQL Schema Inspection

### **View Generated Tables**

```sql
-- List all Marten tables
SELECT tablename
FROM pg_tables
WHERE schemaname = 'public'
  AND tablename LIKE 'mt_%';
```

### **View Table Structure**

```sql
-- Describe User table
\d public.mt_doc_user

-- Or using SQL
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'mt_doc_user';
```

### **View Indexes**

```sql
-- All indexes on User table
SELECT
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename = 'mt_doc_user';
```

### **View Constraints**

```sql
-- Unique constraints
SELECT
    conname AS constraint_name,
    contype AS constraint_type
FROM pg_constraint
WHERE conrelid = 'public.mt_doc_user'::regclass;
```

---

## 🎓 Hands-On Exercises

### **Exercise 1: Optimized User Schema**

```csharp
public record User
{
    public Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    opts.Schema.For<User>()
        // Unique email
        .UniqueIndex(x => x.Email)

        // Index for name searches
        .Index(x => x.LastName)
        .Index(x => x.FirstName)

        // Full-text search on names
        .FullTextIndex(x => x.FirstName, x => x.LastName)

        // Soft deletes
        .SoftDeleted();
});
```

### **Exercise 2: E-Commerce Product Schema**

```csharp
public record Product
{
    public Guid Id { get; init; }
    public required string SKU { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public decimal Price { get; init; }
    public List<string> Tags { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    opts.Schema.For<Product>()
        // Unique SKU
        .UniqueIndex(x => x.SKU)

        // Price range queries
        .Index(x => x.Price)

        // Full-text search
        .FullTextIndex(x => x.Name, x => x.Description)

        // Tag searches
        .GinIndexJsonData()

        // Soft deletes
        .SoftDeleted();
});
```

### **Exercise 3: Custom ID Generation**

```csharp
// Order ID format: ORD-2024-00001
public class OrderIdGenerator : IIdGeneration
{
    private int _counter = 0;

    public IEnumerable<Type> KeyTypes => new[] { typeof(string) };

    public object Assign(ITenant tenant, Type documentType, object document, IMartenSession session)
    {
        var year = DateTime.UtcNow.Year;
        var number = Interlocked.Increment(ref _counter);
        var id = $"ORD-{year}-{number:D5}";

        if (document is Order order)
        {
            return order with { Id = id };
        }
        return document;
    }

    public string ToJson(object id) => id.ToString()!;
}

public record Order
{
    public string Id { get; init; } = "";
    public decimal Total { get; init; }
}

opts.Schema.For<Order>()
    .IdStrategy(new OrderIdGenerator());
```

---

## 💡 Best Practices

### **1. Choose the Right ID Strategy**

```csharp
// ✅ GOOD - Client-generated GUIDs for most cases
public record User
{
    public Guid Id { get; init; } = Guid.NewGuid();
}

// ✅ GOOD - Sequential IDs for ordered lists
public record LogEntry
{
    public long Id { get; init; }
}

// ✅ GOOD - Hi-Lo for best of both worlds
opts.Schema.For<User>()
    .IdStrategy(new HiLoIdGeneration(...));
```

### **2. Index Strategy**

```csharp
// ✅ Index frequently queried fields
opts.Schema.For<User>()
    .Index(x => x.Email) // Used in WHERE clauses

// ✅ Unique constraints for business rules
opts.Schema.For<User>()
    .UniqueIndex(x => x.Email) // Email must be unique

// ❌ Don't over-index
// Each index slows down writes
```

### **3. Soft Deletes for Audit Trails**

```csharp
// ✅ Use soft deletes when you need history
opts.Schema.For<Order>()
    .SoftDeleted(); // Keep cancelled orders

// ❌ Don't use for everything
// Can bloat database
```

### **4. Schema Versioning**

```csharp
// ✅ GOOD - Version your documents
public record UserV1
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
}

public record UserV2
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
}

// Handle both versions
opts.Schema.For<UserV1>().DocumentAlias("user");
opts.Schema.For<UserV2>().DocumentAlias("user");
```

---

## 📊 Schema Management Checklist

**Development:**
- [ ] Use `AutoCreate.All` for rapid iteration
- [ ] Add indexes as queries slow down
- [ ] Use unique constraints for business rules

**Staging:**
- [ ] Switch to `AutoCreate.CreateOnly`
- [ ] Generate migration scripts
- [ ] Test migrations on staging data

**Production:**
- [ ] Use `AutoCreate.None`
- [ ] Apply migrations manually
- [ ] Monitor index usage
- [ ] Review query performance

---

## 🎓 What You've Learned

Fantastic progress! You now understand:

- ✅ **ID strategies** - Client, server, Hi-Lo, custom generation
- ✅ **Indexes** - Simple, composite, GIN, full-text
- ✅ **Unique constraints** - Email uniqueness, composite keys
- ✅ **Schema customization** - Table names, schemas, computed indexes
- ✅ **Soft deletes** - Keep deletion history
- ✅ **Migration strategies** - Development vs. production

---

## 🚀 Next Steps

Ready for more? In **[Chapter 04 - Sessions](TUTORIAL-04-Sessions.md)**, we'll explore:

- Session types (Lightweight vs. Identity Map vs. Dirty Tracking)
- Unit of Work patterns
- Transaction management
- Session lifetimes and best practices

**Continue to [Chapter 04 - Sessions](TUTORIAL-04-Sessions.md) →**

---

**Questions?** Review this chapter or go back to [Chapter 02 - Querying](TUTORIAL-02-Querying.md).
