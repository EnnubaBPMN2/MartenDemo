# 📚 Marten Tutorial - Complete Guide

Welcome to the comprehensive **Marten** tutorial! This hands-on guide teaches you everything from document database basics to advanced event sourcing with PostgreSQL.

---

## 🎯 What You'll Learn

This tutorial covers **all aspects of Marten**, including:

- ✅ Document database fundamentals
- ✅ Advanced querying with LINQ
- ✅ Schema management and indexing
- ✅ Session types and unit of work patterns
- ✅ Optimistic concurrency control
- ✅ Event sourcing and aggregates
- ✅ Projections and read models
- ✅ Production deployment and best practices

---

## 🚀 Quick Start

### **Prerequisites**

- **.NET 8.0+** installed
- **PostgreSQL 14+** running
- Basic knowledge of C# and PostgreSQL

### **Setup**

```bash
# 1. Clone the repository
git clone <your-repo-url>
cd MartenDemo

# 2. Configure connection string
# Edit appsettings.json or set environment variable:
export CONN="Host=localhost;Port=5432;Database=marten_demo;Username=postgres;Password=yourpassword"

# 3. Restore packages
dotnet restore

# 4. Run the interactive demo
dotnet run
```

---

## 📖 Tutorial Chapters

### **[Chapter 00 - Introduction](TUTORIAL-00-Introduction.md)**
Setup, prerequisites, and overview of what you'll learn.

**Topics:**
- What is Marten?
- Why use it?
- Installation and configuration
- Repository structure

---

### **[Chapter 01 - Document Database Basics](TUTORIAL-01-Basics.md)**
Learn the fundamentals of document storage with Marten.

**Topics:**
- What is a document database?
- DocumentStore and sessions
- Basic CRUD operations
- PostgreSQL internals

**Code Example:**
```csharp
await using var session = store.LightweightSession();

var user = new User { Id = Guid.NewGuid(), Name = "Alice", Email = "alice@example.com" };
session.Store(user);
await session.SaveChangesAsync();

var loaded = await session.LoadAsync<User>(user.Id);
```

---

### **[Chapter 02 - Advanced Querying](TUTORIAL-02-Querying.md)**
Master LINQ queries, compiled queries, and full-text search.

**Topics:**
- Filtering and ordering
- Paging and aggregations
- Querying nested properties
- Compiled queries for performance
- Full-text search

**Code Example:**
```csharp
var users = await session.Query<User>()
    .Where(u => u.Email.EndsWith("@example.com"))
    .OrderBy(u => u.Name)
    .Take(10)
    .ToListAsync();
```

---

### **[Chapter 03 - Identity & Schema Management](TUTORIAL-03-Identity-Schema.md)**
Understand ID strategies, indexes, and schema customization.

**Topics:**
- ID generation strategies
- Creating indexes
- Unique constraints
- Soft deletes
- Schema migrations

**Code Example:**
```csharp
opts.Schema.For<User>()
    .UniqueIndex(x => x.Email)
    .Index(x => x.Name)
    .FullTextIndex(x => x.Name, x => x.Email);
```

---

### **[Chapter 04 - Sessions & Unit of Work](TUTORIAL-04-Sessions.md)**
Learn about the three session types and transaction management.

**Topics:**
- Lightweight sessions (fastest)
- Identity map sessions
- Dirty tracked sessions
- Transaction management
- Session lifetimes

**Code Example:**
```csharp
// Lightweight - best for most scenarios
await using var session = store.LightweightSession();

// Identity map - single instance per ID
await using var session = store.OpenSession();

// Dirty tracking - automatic change detection
await using var session = store.DirtyTrackedSession();
```

---

### **[Chapter 05 - Optimistic Concurrency](TUTORIAL-05-Concurrency.md)**
Prevent lost updates with version-based conflict detection.

**Topics:**
- Understanding concurrency problems
- Optimistic concurrency control
- Version tracking
- Conflict resolution strategies
- Retry logic

**Code Example:**
```csharp
await using var session = store.LightweightSession();
session.UseOptimisticConcurrency();

var account = await session.LoadAsync<BankAccount>(id);
var updated = account with { Balance = account.Balance - 100 };
session.Store(updated);

try
{
    await session.SaveChangesAsync();
}
catch (ConcurrencyException)
{
    // Handle conflict
}
```

---

### **[Chapter 06 - Event Sourcing Fundamentals](TUTORIAL-06-EventSourcing.md)**
Store events instead of state - unlock audit trails and time travel.

**Topics:**
- Event sourcing concepts
- Event streams and aggregates
- Appending and replaying events
- Command-event patterns
- When to use event sourcing

**Code Example:**
```csharp
// Start an event stream
session.Events.StartStream<BankAccount>(
    accountId,
    new AccountOpened(accountId, "ACC-001", 1000m)
);

// Append events
session.Events.Append(accountId, new MoneyDeposited(accountId, 500m));
await session.SaveChangesAsync();

// Rebuild aggregate from events
var account = await session.Events.AggregateStreamAsync<BankAccount>(accountId);
Console.WriteLine($"Balance: ${account.Balance}"); // $1500
```

---

### **[Chapter 07 - Projections & Read Models](TUTORIAL-07-Projections.md)**
Transform event streams into queryable read models.

**Topics:**
- Inline vs. async projections
- Single-stream projections
- Multi-stream projections
- Rebuilding projections
- CQRS pattern

**Code Example:**
```csharp
public class AccountBalanceProjection : SingleStreamProjection<AccountBalance>
{
    public AccountBalance Create(AccountOpened e)
    {
        return new AccountBalance
        {
            Id = e.AccountId,
            Balance = e.InitialBalance
        };
    }

    public void Apply(MoneyDeposited e, AccountBalance view)
    {
        view.Balance += e.Amount;
    }
}

// Query the projection
var balance = await session.LoadAsync<AccountBalance>(accountId);
```

---

### **[Chapter 08 - Advanced Topics](TUTORIAL-08-Advanced.md)**
Production-ready patterns and advanced features.

**Topics:**
- Multi-tenancy
- Document patching
- Schema migrations
- Performance optimization
- Production configuration
- Best practices roundup

**Code Example:**
```csharp
// Document patching
session.Patch<User>(userId).Set(u => u.Email, "new@example.com");
await session.SaveChangesAsync();

// Multi-tenancy
await using var session = store.LightweightSession("tenant-1");
var users = await session.Query<User>().ToListAsync(); // Only tenant-1 data
```

---

## 🎮 Interactive Demo

Run the application to explore concepts interactively:

```bash
dotnet run
```

**Features:**
- Chapter-by-chapter examples
- Data seeding and reset
- Database statistics
- Interactive menu system

---

## 📁 Project Structure

```
MartenDemo/
├── Program.cs                      # Interactive demo application
├── appsettings.json                # Configuration
│
├── Models/                         # Domain models
│   ├── Product.cs
│   ├── Order.cs
│   └── Contact.cs
│
├── EventSourcing/                  # Event sourcing examples
│   ├── Events/
│   │   └── AccountEvents.cs
│   ├── Aggregates/
│   │   └── BankAccount.cs
│   └── Projections/
│       ├── AccountBalanceProjection.cs
│       └── TransactionHistoryProjection.cs
│
├── Helpers/                        # Utility classes
│   ├── DataSeeder.cs
│   └── DatabaseReset.cs
│
└── TUTORIAL-*.md                   # Tutorial chapters
```

---

## 💡 Key Concepts

### **Document Database**
Store .NET objects as JSON in PostgreSQL with type-safe LINQ queries.

### **Event Sourcing**
Store immutable events representing what happened, rebuild state by replaying.

### **CQRS**
Separate read models (projections) from write models (events/commands).

### **Projections**
Transform events into queryable views optimized for reads.

---

## 🔧 Configuration

### **appsettings.json**
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=marten_demo;Username=postgres;Password=yourpassword"
  },
  "Marten": {
    "AutoCreateSchemaObjects": "All"
  }
}
```

### **Environment Variable**
```bash
export CONN="Host=localhost;Database=marten_demo;Username=postgres;Password=yourpassword"
```

---

## 📊 What Marten Creates in PostgreSQL

### **Document Tables**
```sql
CREATE TABLE mt_doc_user (
    id UUID PRIMARY KEY,
    data JSONB NOT NULL,
    mt_version UUID NOT NULL,
    mt_last_modified TIMESTAMP
);
```

### **Event Tables**
```sql
CREATE TABLE mt_events (
    seq_id BIGSERIAL PRIMARY KEY,
    stream_id UUID NOT NULL,
    version INTEGER NOT NULL,
    data JSONB NOT NULL,
    type VARCHAR NOT NULL,
    timestamp TIMESTAMP
);
```

---

## 🎓 Learning Path

### **Beginner (Start Here)**
1. Chapter 00 - Introduction
2. Chapter 01 - Basics
3. Chapter 02 - Querying
4. Chapter 03 - Identity & Schema

### **Intermediate**
5. Chapter 04 - Sessions
6. Chapter 05 - Concurrency

### **Advanced**
7. Chapter 06 - Event Sourcing
8. Chapter 07 - Projections
9. Chapter 08 - Advanced Topics

---

## 🛠️ Development Workflow

### **1. Seed Data**
```bash
dotnet run
# Select: 9 → Data Management → 1 → Seed Sample Data
```

### **2. Explore Examples**
```bash
# Select chapters 1-8 to run examples
```

### **3. Reset Database**
```bash
# Select: 9 → Data Management → 4 → Complete Reset
```

---

## 📚 Additional Resources

**Official Documentation:**
- [Marten Docs](https://martendb.io/)
- [GitHub Repository](https://github.com/JasperFx/marten)
- [Event Sourcing Guide](https://martendb.io/events/)

**Related Topics:**
- Event Sourcing patterns
- CQRS architecture
- Domain-Driven Design
- PostgreSQL JSONB optimization

---

## 🤝 Contributing

Found an issue or want to improve the tutorial?
1. Fork the repository
2. Create a feature branch
3. Submit a pull request

---

## 📝 License

This tutorial is open source and available for educational purposes.

---

## 🙏 Acknowledgments

Built with [Marten](https://martendb.io/) by JasperFx team.

Special thanks to:
- Jeremy D. Miller (Marten creator)
- The JasperFx community
- PostgreSQL team

---

## 🚀 Next Steps

1. **Start with [Chapter 00 - Introduction](TUTORIAL-00-Introduction.md)**
2. **Run the interactive demo**: `dotnet run`
3. **Explore each chapter sequentially**
4. **Build your own project!**

---

**Happy Learning! 🎉**

---

**Questions or feedback?** Open an issue on GitHub!
