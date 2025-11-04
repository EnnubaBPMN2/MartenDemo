# 🚀 Chapter 08 - Advanced Topics & Production Patterns

This final chapter covers advanced Marten features and best practices for production deployment.

---

## 🎯 Chapter Objectives

By the end of this chapter, you will:

- ✅ Implement multi-tenancy
- ✅ Use document patching
- ✅ Manage schema migrations
- ✅ Apply performance optimizations
- ✅ Configure for production
- ✅ Master best practices

---

## 🏢 Multi-Tenancy

Host multiple tenants (customers) in the same database with data isolation.

### **Tenancy Strategies**

#### **1. Conjoined Tenancy (Shared Tables)**

All tenants share the same tables with a tenant identifier.

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Enable conjoined multi-tenancy
    opts.Policies.AllDocumentsAreMultiTenanted();

    // Or per document type
    opts.Schema.For<User>().MultiTenanted();
});
```

**Generated Schema:**
```sql
CREATE TABLE mt_doc_user (
    id UUID PRIMARY KEY,
    data JSONB NOT NULL,
    tenant_id VARCHAR NOT NULL,  -- Added!
    ...
);

CREATE INDEX ON mt_doc_user(tenant_id);
```

**Usage:**
```csharp
// Set tenant for session
await using var session = store.LightweightSession("tenant-1");

var user = new User { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@tenant1.com" };
session.Store(user);
await session.SaveChangesAsync();

// Queries automatically filtered by tenant
var users = await session.Query<User>().ToListAsync();
// Only returns users for tenant-1
```

#### **2. Separate Schemas per Tenant**

Each tenant gets its own PostgreSQL schema.

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Each tenant → separate schema
    opts.Policies.AllDocumentsAreMultiTenanted();
    opts.Policies.ForAllTenants(tenant =>
    {
        tenant.DatabaseSchemaName = $"tenant_{tenant.TenantId}";
    });
});
```

**Generated:**
```sql
-- Tenant 1
CREATE SCHEMA tenant_1;
CREATE TABLE tenant_1.mt_doc_user (...);

-- Tenant 2
CREATE SCHEMA tenant_2;
CREATE TABLE tenant_2.mt_doc_user (...);
```

#### **3. Separate Databases per Tenant**

Each tenant gets its own database.

```csharp
public class TenantConnectionFactory : ITenantConnectionFactory
{
    private readonly Dictionary<string, string> _connectionStrings = new()
    {
        ["tenant-1"] = "Host=localhost;Database=tenant1_db;...",
        ["tenant-2"] = "Host=localhost;Database=tenant2_db;...",
        ["tenant-3"] = "Host=localhost;Database=tenant3_db;..."
    };

    public NpgsqlConnection GetConnection(string tenantId)
    {
        var connString = _connectionStrings[tenantId];
        return new NpgsqlConnection(connString);
    }
}

var store = DocumentStore.For(opts =>
{
    opts.MultiTenantedWithSingleServer(new TenantConnectionFactory());
});
```

### **Multi-Tenant Example**

```csharp
public static async Task MultiTenancyDemo(IDocumentStore store)
{
    // Tenant 1 operations
    await using (var session = store.LightweightSession("tenant-1"))
    {
        var user = new User { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@company1.com" };
        session.Store(user);
        await session.SaveChangesAsync();
    }

    // Tenant 2 operations
    await using (var session = store.LightweightSession("tenant-2"))
    {
        var user = new User { Id = Guid.NewGuid(), Name = "Bob", Email = "bob@company2.com" };
        session.Store(user);
        await session.SaveChangesAsync();
    }

    // Query tenant 1 - only sees their data
    await using (var session = store.LightweightSession("tenant-1"))
    {
        var users = await session.Query<User>().ToListAsync();
        Console.WriteLine($"Tenant 1 users: {users.Count}"); // 1 (Alice)
    }

    // Query tenant 2 - only sees their data
    await using (var session = store.LightweightSession("tenant-2"))
    {
        var users = await session.Query<User>().ToListAsync();
        Console.WriteLine($"Tenant 2 users: {users.Count}"); // 1 (Bob)
    }
}
```

### **Choosing a Strategy**

| Strategy | Data Isolation | Performance | Complexity |
|----------|---------------|-------------|------------|
| **Shared Tables** | ⭐⭐ Low | ⭐⭐⭐ Best | ⭐ Simple |
| **Separate Schemas** | ⭐⭐⭐ Good | ⭐⭐ Good | ⭐⭐ Moderate |
| **Separate Databases** | ⭐⭐⭐ Best | ⭐ Lower | ⭐⭐⭐ Complex |

---

## 🔧 Document Patching

Update specific fields without loading the entire document.

### **Why Patch?**

```csharp
// ❌ SLOW - Load entire document
var user = await session.LoadAsync<User>(userId);
var updated = user with { Email = "new@example.com" };
session.Store(updated);
await session.SaveChangesAsync();

// ✅ FAST - Patch only one field
session.Patch<User>(userId).Set(u => u.Email, "new@example.com");
await session.SaveChangesAsync();
```

### **Basic Patching**

```csharp
public static async Task PatchingExamples(IDocumentStore store, Guid userId)
{
    await using var session = store.LightweightSession();

    // Set a property
    session.Patch<User>(userId).Set(u => u.Email, "updated@example.com");

    // Increment a number
    session.Patch<Order>(orderId).Increment(o => o.Quantity, 5);

    // Append to array
    session.Patch<Contact>(contactId).Append(c => c.PhoneNumbers, "555-1234");

    // Remove from array
    session.Patch<Contact>(contactId).Remove(c => c.PhoneNumbers, "555-0000");

    // Rename field (advanced)
    session.Patch<User>(userId).Rename("OldField", u => u.NewField);

    // Commit all patches
    await session.SaveChangesAsync();
}
```

### **Conditional Patching**

```csharp
// Patch only if condition matches
session.Patch<Product>(productId)
    .Set(p => p.Price, 99.99m)
    .Where(p => p.Price < 100m); // Only if current price < 100
```

### **Batch Patching**

```csharp
// Patch multiple documents
await using var session = store.LightweightSession();

// All users with @oldcompany.com → @newcompany.com
var userIds = await session.Query<User>()
    .Where(u => u.Email.EndsWith("@oldcompany.com"))
    .Select(u => u.Id)
    .ToListAsync();

foreach (var id in userIds)
{
    session.Patch<User>(id).Set(u => u.Email,
        u => u.Email.Replace("@oldcompany.com", "@newcompany.com"));
}

await session.SaveChangesAsync();
```

---

## 📦 Schema Migrations

### **Development Workflow**

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);
    opts.AutoCreateSchemaObjects = AutoCreate.All; // Auto-create during dev
});
```

### **Production Workflow**

```csharp
// 1. Generate migration script
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);
    opts.AutoCreateSchemaObjects = AutoCreate.None; // Manual in production
});

// 2. Export DDL
var writer = new StringWriter();
store.Schema.WriteDDL(writer);
var ddl = writer.ToString();

File.WriteAllText("migrations/001_initial.sql", ddl);

// 3. Apply manually
// psql -U postgres -d marten_db -f migrations/001_initial.sql
```

### **Schema Diff**

```csharp
// Generate diff between code and database
var patch = await store.Schema.CreateMigrationAsync();

if (patch.Difference != SchemaPatchDifference.None)
{
    Console.WriteLine("Schema changes detected:");
    Console.WriteLine(patch.UpdateDDL);

    // Apply if needed
    await store.Schema.ApplyAllConfiguredChangesToDatabaseAsync();
}
```

### **Versioned Migrations**

```csharp
// Migration 001: Initial schema
public class Migration_001_Initial : ISchemaChange
{
    public void Apply(IDocumentStore store)
    {
        store.Schema.For<User>()
            .Index(x => x.Email);
    }
}

// Migration 002: Add index
public class Migration_002_AddNameIndex : ISchemaChange
{
    public void Apply(IDocumentStore store)
    {
        store.Schema.For<User>()
            .Index(x => x.Name);
    }
}

// Apply migrations
var migrations = new List<ISchemaChange>
{
    new Migration_001_Initial(),
    new Migration_002_AddNameIndex()
};

foreach (var migration in migrations)
{
    migration.Apply(store);
}

await store.Schema.ApplyAllConfiguredChangesToDatabaseAsync();
```

---

## ⚡ Performance Optimization

### **1. Use Appropriate Session Type**

```csharp
// ✅ Lightweight for simple operations
await using var session = store.LightweightSession();

// ✅ OpenSession when needed
await using var session = store.OpenSession();

// ❌ Don't default to DirtyTracked
// await using var session = store.DirtyTrackedSession();
```

### **2. Batch Operations**

```csharp
// ✅ GOOD - Single transaction
await using var session = store.LightweightSession();
foreach (var user in users)
{
    session.Store(user);
}
await session.SaveChangesAsync(); // Once

// ❌ BAD - N transactions
foreach (var user in users)
{
    await using var session = store.LightweightSession();
    session.Store(user);
    await session.SaveChangesAsync(); // N times!
}
```

### **3. Use Compiled Queries**

```csharp
// ✅ Cache query compilation
public static readonly CompiledQuery<User> ByEmail =
    (session, email) => session.Query<User>()
        .FirstOrDefault(u => u.Email == email);

var user = await session.QueryAsync(ByEmail, "test@example.com");
```

### **4. Add Indexes**

```csharp
opts.Schema.For<User>()
    .Index(x => x.Email)          // Simple index
    .Index(x => x.Name)           // Another simple index
    .Index(x => x.Name, x => x.Email); // Composite index
```

### **5. Use Projections for Reads**

```csharp
// ❌ SLOW - Replay events
var account = await session.Events.AggregateStreamAsync<BankAccount>(id);

// ✅ FAST - Query projection
var balance = await session.LoadAsync<AccountBalance>(id);
```

### **6. Enable Connection Pooling**

```csharp
var connectionString = "Host=localhost;Port=5432;Database=marten_db;" +
                       "Username=postgres;Password=password;" +
                       "Pooling=true;MinPoolSize=1;MaxPoolSize=20;";
```

---

## 🏗️ Production Configuration

### **Complete Production Setup**

```csharp
public static IDocumentStore CreateProductionStore(IConfiguration config)
{
    var connectionString = config.GetConnectionString("Postgres");

    return DocumentStore.For(opts =>
    {
        // Connection
        opts.Connection(connectionString);

        // Schema management
        opts.AutoCreateSchemaObjects = AutoCreate.None; // Manual migrations

        // Performance
        opts.UseDefaultSerialization(
            EnumStorage.AsString,
            Casing.CamelCase
        );

        // Policies
        opts.Policies.AllDocumentsAreMultiTenanted(); // If needed

        // Indexes
        opts.Schema.For<User>()
            .Index(x => x.Email)
            .UniqueIndex(x => x.Email);

        opts.Schema.For<Product>()
            .Index(x => x.SKU)
            .FullTextIndex(x => x.Name, x => x.Description);

        // Event sourcing
        opts.Events.DatabaseSchemaName = "events";

        // Projections
        opts.Projections.Add<AccountBalanceProjection>(ProjectionLifecycle.Inline);
        opts.Projections.Add<ReportingProjection>(ProjectionLifecycle.Async);

        // Logging (use your logger)
        opts.Logger(new ConsoleMartenLogger());
    });
}
```

### **ASP.NET Core Integration**

```csharp
// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register DocumentStore as singleton
    services.AddMarten(opts =>
    {
        opts.Connection(Configuration.GetConnectionString("Postgres"));
        opts.AutoCreateSchemaObjects = AutoCreate.None;

        // Configuration...
    })
    .UseLightweightSessions(); // Scoped sessions per request

    // Or register manually
    services.AddSingleton<IDocumentStore>(sp =>
    {
        return CreateProductionStore(Configuration);
    });
}

// Controller
public class UsersController : ControllerBase
{
    private readonly IDocumentSession _session;

    public UsersController(IDocumentSession session)
    {
        _session = session; // Injected per request
    }

    [HttpGet("{id}")]
    public async Task<User?> GetUser(Guid id)
    {
        return await _session.LoadAsync<User>(id);
    }
}
```

---

## 🎓 Best Practices Roundup

### **Architecture**

```csharp
// ✅ Use documents for simple entities
public record Product { ... }

// ✅ Use events for complex business logic
session.Events.Append(orderId, new OrderPlaced(...));

// ✅ Separate read and write models (CQRS)
// Write: Events
// Read: Projections
```

### **Performance**

```csharp
// ✅ Batch operations
// ✅ Use compiled queries
// ✅ Add indexes to queried fields
// ✅ Use projections for complex reads
// ✅ Profile your queries
```

### **Consistency**

```csharp
// ✅ Use optimistic concurrency for critical operations
session.UseOptimisticConcurrency();

// ✅ Implement retry logic
// ✅ Use transactions appropriately
```

### **Schema Management**

```csharp
// ✅ Auto-create in development
// ✅ Manual migrations in production
// ✅ Version your schemas
// ✅ Test migrations in staging
```

### **Testing**

```csharp
// ✅ Use in-memory or test database
// ✅ Reset database between tests
// ✅ Test projections separately
// ✅ Test event replay
```

---

## 🚨 Common Pitfalls

### **1. Long-Lived Sessions**

```csharp
// ❌ BAD
private readonly IDocumentSession _session;

public MyService(IDocumentStore store)
{
    _session = store.LightweightSession(); // Lives forever!
}

// ✅ GOOD
public async Task DoWork()
{
    await using var session = _store.LightweightSession();
    // Short-lived
}
```

### **2. Forgetting SaveChangesAsync**

```csharp
// ❌ BAD - Nothing saved!
session.Store(user);

// ✅ GOOD
session.Store(user);
await session.SaveChangesAsync();
```

### **3. Over-Indexing**

```csharp
// ❌ BAD - Too many indexes slow writes
opts.Schema.For<User>()
    .Index(x => x.Email)
    .Index(x => x.Name)
    .Index(x => x.FirstName)
    .Index(x => x.LastName)
    .Index(x => x.Phone)
    // ... 10 more indexes

// ✅ GOOD - Index what you query
opts.Schema.For<User>()
    .UniqueIndex(x => x.Email)  // For login
    .Index(x => x.LastName);     // For search
```

### **4. Not Using Projections**

```csharp
// ❌ BAD - Replay 10,000 events every time
var account = await session.Events.AggregateStreamAsync<BankAccount>(id);

// ✅ GOOD - Query projection
var balance = await session.LoadAsync<AccountBalance>(id);
```

---

## 📊 Production Checklist

**Before Going Live:**

- [ ] Change `AutoCreateSchemaObjects` to `None`
- [ ] Create migration scripts
- [ ] Add indexes to frequently queried fields
- [ ] Enable connection pooling
- [ ] Configure logging
- [ ] Implement health checks
- [ ] Set up monitoring
- [ ] Test backup/restore
- [ ] Load test your queries
- [ ] Review security settings

**Security:**

- [ ] Don't commit connection strings
- [ ] Use environment variables
- [ ] Restrict database permissions
- [ ] Enable SSL for connections
- [ ] Audit access logs

**Monitoring:**

- [ ] Query performance metrics
- [ ] Connection pool stats
- [ ] Projection daemon health
- [ ] Event store growth rate
- [ ] Projection lag time

---

## 🎓 What You've Learned

Congratulations! You've completed the Marten tutorial and now understand:

- ✅ **Document database** - CRUD, querying, schema management
- ✅ **Event sourcing** - Events, streams, aggregates
- ✅ **Projections** - Read models from events
- ✅ **Advanced features** - Multi-tenancy, patching, migrations
- ✅ **Production patterns** - Performance, configuration, best practices

---

## 🎉 Next Steps

You're now equipped to build production applications with Marten!

**Continue Learning:**
- Read the [official Marten docs](https://martendb.io/)
- Explore [sample applications](https://github.com/JasperFx/marten)
- Join the community discussions
- Build your own project!

**Experiment:**
- Add more domain models to this demo
- Implement complex event sourcing scenarios
- Try different projection strategies
- Build a full CQRS application

---

## 📚 Further Reading

**Official Resources:**
- [Marten Documentation](https://martendb.io/)
- [GitHub Repository](https://github.com/JasperFx/marten)
- [Jeremy Miller's Blog](https://jeremydmiller.com/)

**Related Topics:**
- Event Sourcing patterns
- CQRS architecture
- Domain-Driven Design
- PostgreSQL optimization

---

## 🙏 Thank You!

Thank you for completing this Marten tutorial. You've learned the fundamentals and are ready to build amazing applications!

**Happy coding!** 🚀

---

**Questions?** Review any chapter or start building your own project!

**[← Back to Introduction](TUTORIAL-00-Introduction.md)**
