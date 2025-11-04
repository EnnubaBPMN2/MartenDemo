# 🔍 Chapter 02 - Advanced Querying

Now that you understand the basics, let's explore Marten's powerful querying capabilities. Marten translates LINQ queries into optimized PostgreSQL queries, giving you type safety and performance.

---

## 🎯 Chapter Objectives

By the end of this chapter, you will:

- ✅ Master LINQ queries with Marten
- ✅ Use compiled queries for performance
- ✅ Implement full-text search
- ✅ Query nested properties and collections
- ✅ Understand query execution and optimization
- ✅ Use advanced filtering, aggregation, and paging

---

## 📚 Query Fundamentals

### **How Marten Queries Work**

```csharp
// Your C# LINQ
var users = await session.Query<User>()
    .Where(u => u.Email.Contains("@example.com"))
    .OrderBy(u => u.Name)
    .ToListAsync();

// Translated to PostgreSQL
// SELECT data FROM mt_doc_user
// WHERE data->>'Email' LIKE '%@example.com%'
// ORDER BY data->>'Name'
```

**Key Concepts:**
1. **Deferred Execution** - Query builds until you execute (ToList, First, etc.)
2. **Translation** - LINQ operators become SQL queries
3. **Type Safety** - Compile-time checking of your queries
4. **Performance** - Optimized PostgreSQL queries with indexes

---

## 💻 Basic LINQ Queries

### **1. Filtering with Where**

```csharp
public static async Task WhereExamples(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // Simple equality
    var user = await session.Query<User>()
        .Where(u => u.Email == "hermann@example.com")
        .FirstOrDefaultAsync();

    // String operations
    var gmailUsers = await session.Query<User>()
        .Where(u => u.Email.EndsWith("@gmail.com"))
        .ToListAsync();

    // Multiple conditions (AND)
    var filtered = await session.Query<User>()
        .Where(u => u.Name.StartsWith("A") && u.Email.Contains("example"))
        .ToListAsync();

    // OR conditions
    var either = await session.Query<User>()
        .Where(u => u.Name == "Alice" || u.Name == "Bob")
        .ToListAsync();

    // Negation
    var notExample = await session.Query<User>()
        .Where(u => !u.Email.EndsWith("@example.com"))
        .ToListAsync();
}
```

**Supported String Operations:**
- `Contains()` → `LIKE '%value%'`
- `StartsWith()` → `LIKE 'value%'`
- `EndsWith()` → `LIKE '%value'`
- `==` → `=`
- `!=` → `<>`

### **2. Ordering**

```csharp
public static async Task OrderingExamples(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // Order ascending
    var ascending = await session.Query<User>()
        .OrderBy(u => u.Name)
        .ToListAsync();

    // Order descending
    var descending = await session.Query<User>()
        .OrderByDescending(u => u.Email)
        .ToListAsync();

    // Multiple sort keys
    var multiSort = await session.Query<User>()
        .OrderBy(u => u.Name)
        .ThenByDescending(u => u.Email)
        .ToListAsync();
}
```

### **3. Paging**

```csharp
public static async Task PagingExamples(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    int pageSize = 10;
    int pageNumber = 2; // Zero-based

    // Skip and Take
    var page = await session.Query<User>()
        .OrderBy(u => u.Name)
        .Skip(pageNumber * pageSize)
        .Take(pageSize)
        .ToListAsync();

    // Get total count
    var totalCount = await session.Query<User>().CountAsync();

    Console.WriteLine($"Page {pageNumber + 1}, showing {page.Count} of {totalCount} users");
}
```

**💡 Paging Best Practice:**
```csharp
public record PagedResult<T>(List<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => PageNumber < TotalPages - 1;
    public bool HasPreviousPage => PageNumber > 0;
}

public static async Task<PagedResult<User>> GetUsersPagedAsync(
    IDocumentStore store, int pageNumber, int pageSize)
{
    await using var session = store.LightweightSession();

    var items = await session.Query<User>()
        .OrderBy(u => u.Name)
        .Skip(pageNumber * pageSize)
        .Take(pageSize)
        .ToListAsync();

    var totalCount = await session.Query<User>().CountAsync();

    return new PagedResult<User>(items, totalCount, pageNumber, pageSize);
}
```

### **4. Aggregations**

```csharp
public static async Task AggregationExamples(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // Count
    var totalUsers = await session.Query<User>().CountAsync();
    var gmailCount = await session.Query<User>()
        .CountAsync(u => u.Email.EndsWith("@gmail.com"));

    // Any / All
    var hasUsers = await session.Query<User>().AnyAsync();
    var allHaveEmail = await session.Query<User>()
        .AllAsync(u => !string.IsNullOrEmpty(u.Email));

    // First / Single
    var first = await session.Query<User>()
        .FirstOrDefaultAsync(); // Returns first or null

    var single = await session.Query<User>()
        .Where(u => u.Email == "unique@example.com")
        .SingleOrDefaultAsync(); // Throws if multiple match
}
```

---

## 🚀 Advanced Querying

### **1. Querying Nested Properties**

Let's add a more complex document:

```csharp
public record Address(string Street, string City, string ZipCode);

public record Contact
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public Address? HomeAddress { get; init; }
    public List<string> PhoneNumbers { get; init; } = new();
}
```

**Query nested objects:**

```csharp
public static async Task NestedPropertyExamples(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // Query by nested property
    var inSeattle = await session.Query<Contact>()
        .Where(c => c.HomeAddress!.City == "Seattle")
        .ToListAsync();

    // Multiple nested properties
    var specificAddress = await session.Query<Contact>()
        .Where(c => c.HomeAddress!.City == "Seattle"
                 && c.HomeAddress.ZipCode.StartsWith("98"))
        .ToListAsync();
}
```

**PostgreSQL Translation:**
```sql
SELECT data FROM mt_doc_contact
WHERE data->'HomeAddress'->>'City' = 'Seattle'
  AND data->'HomeAddress'->>'ZipCode' LIKE '98%';
```

### **2. Querying Collections**

```csharp
public static async Task CollectionQueryExamples(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // Any item matches
    var hasLocalNumber = await session.Query<Contact>()
        .Where(c => c.PhoneNumbers.Any(p => p.StartsWith("206")))
        .ToListAsync();

    // Contains specific value
    var specificPhone = await session.Query<Contact>()
        .Where(c => c.PhoneNumbers.Contains("555-1234"))
        .ToListAsync();

    // Collection count
    var multiplePhones = await session.Query<Contact>()
        .Where(c => c.PhoneNumbers.Count > 1)
        .ToListAsync();
}
```

### **3. IsOneOf - Query by Multiple IDs**

```csharp
public static async Task IsOneOfExample(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    var ids = new[] {
        Guid.Parse("a1b2c3d4-..."),
        Guid.Parse("e5f6a7b8-..."),
        Guid.Parse("c9d0e1f2-...")
    };

    // Efficient IN query
    var users = await session.Query<User>()
        .Where(u => u.Id.IsOneOf(ids))
        .ToListAsync();

    // Equivalent SQL: WHERE id IN (...)
}
```

### **4. Select and Projections**

```csharp
public static async Task ProjectionExamples(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // Project to anonymous type
    var emailList = await session.Query<User>()
        .Select(u => new { u.Name, u.Email })
        .ToListAsync();

    // Project to specific type
    var viewModels = await session.Query<User>()
        .Select(u => new UserViewModel(u.Name, u.Email))
        .ToListAsync();

    // Select specific fields (reduces data transfer)
    var names = await session.Query<User>()
        .Select(u => u.Name)
        .ToListAsync();
}

public record UserViewModel(string Name, string Email);
```

**💡 Performance Tip:**
Projections reduce data transfer from PostgreSQL - only selected fields are serialized.

---

## ⚡ Compiled Queries

For frequently-executed queries, **compiled queries** offer significant performance benefits.

### **Why Compiled Queries?**

**Regular LINQ Query:**
1. Parse LINQ expression tree
2. Translate to SQL
3. Execute query
4. Repeat steps 1-2 every time

**Compiled Query:**
1. Parse and translate once
2. Cache the SQL
3. Execute directly (skip parsing)

**Performance Gain:** 20-50% faster for hot paths

### **Basic Compiled Query**

```csharp
public static class UserQueries
{
    // Define compiled query as static field
    public static readonly CompiledQuery<User> ByEmail =
        (session, email) => session.Query<User>()
            .FirstOrDefault(u => u.Email == email);

    // Usage
    public static async Task UseCompiledQuery(IDocumentStore store)
    {
        await using var session = store.LightweightSession();

        var user = await session.QueryAsync(ByEmail, "test@example.com");
    }
}
```

### **Advanced Compiled Queries**

```csharp
public static class CompiledQueryExamples
{
    // Query with multiple parameters
    public static readonly CompiledQuery<User> ByEmailDomain =
        (session, domain) => session.Query<User>()
            .Where(u => u.Email.EndsWith(domain))
            .ToList();

    // With ordering and paging
    public static readonly CompiledQuery<User> PagedByName =
        (session, pageNumber, pageSize) => session.Query<User>()
            .OrderBy(u => u.Name)
            .Skip(pageNumber * pageSize)
            .Take(pageSize)
            .ToList();

    // Complex filter
    public static readonly CompiledQuery<User> SearchUsers =
        (session, searchTerm) => session.Query<User>()
            .Where(u => u.Name.Contains(searchTerm) || u.Email.Contains(searchTerm))
            .OrderBy(u => u.Name)
            .ToList();
}

// Usage
public static async Task UseAdvancedCompiledQueries(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    var gmailUsers = await session.QueryAsync(
        CompiledQueryExamples.ByEmailDomain, "@gmail.com");

    var page = await session.QueryAsync(
        CompiledQueryExamples.PagedByName, pageNumber: 0, pageSize: 10);

    var results = await session.QueryAsync(
        CompiledQueryExamples.SearchUsers, "john");
}
```

**💡 When to Use Compiled Queries:**
- ✅ Frequently executed queries (hot paths)
- ✅ API endpoints hit thousands of times
- ✅ Background jobs running repeatedly
- ❌ One-off queries or rarely used
- ❌ Dynamic queries with many variations

---

## 🔎 Full-Text Search

Marten supports PostgreSQL's powerful full-text search capabilities.

### **Setup: Configure Full-Text Index**

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Configure full-text search on User
    opts.Schema.For<User>().FullTextIndex(x => x.Name);

    // Or multiple properties
    opts.Schema.For<User>().FullTextIndex(x => x.Name, x => x.Email);
});
```

### **Search Queries**

```csharp
public static async Task FullTextSearchExamples(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    // Basic search
    var results = await session.Query<User>()
        .Where(u => u.Search("john smith"))
        .ToListAsync();

    // Search with PlainTextSearch (simpler)
    var plainSearch = await session.Query<User>()
        .Where(u => u.PlainTextSearch("john"))
        .ToListAsync();

    // Search with PhraseSearch (exact phrase)
    var phraseSearch = await session.Query<User>()
        .Where(u => u.PhraseSearch("john smith"))
        .ToListAsync();

    // WebStyle search (Google-like)
    var webSearch = await session.Query<User>()
        .Where(u => u.WebStyleSearch("john OR jane"))
        .ToListAsync();
}
```

**Search Operators (WebStyleSearch):**
- `john jane` - Both terms (AND)
- `john OR jane` - Either term
- `"john smith"` - Exact phrase
- `-spam` - Exclude term

### **Full-Text Search with Ranking**

```csharp
public static async Task RankedSearchExample(IDocumentStore store)
{
    await using var session = store.LightweightSession();

    var searchTerm = "developer";

    var rankedResults = await session.Query<User>()
        .Where(u => u.Search(searchTerm))
        .OrderByDescending(u => u.Rank(searchTerm)) // Order by relevance
        .Take(10)
        .ToListAsync();
}
```

---

## 🔧 Query Optimization

### **1. Use LoadAsync for Known IDs**

```csharp
// ✅ BEST - Direct primary key lookup
var user = await session.LoadAsync<User>(userId);

// ❌ SLOWER - Full query
var user = await session.Query<User>()
    .FirstOrDefaultAsync(u => u.Id == userId);
```

### **2. Add Indexes for Frequently Queried Fields**

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Simple index
    opts.Schema.For<User>().Index(x => x.Email);

    // Unique index
    opts.Schema.For<User>().UniqueIndex(x => x.Email);

    // Composite index
    opts.Schema.For<User>().Index(x => x.Name, x => x.Email);
});
```

**When to Add Indexes:**
- ✅ WHERE clauses on specific fields
- ✅ ORDER BY frequently
- ✅ Unique constraints needed
- ❌ Rarely queried fields
- ❌ High write volume (indexes slow writes)

### **3. Use Projections to Reduce Data Transfer**

```csharp
// ❌ SLOWER - Fetches entire document
var allData = await session.Query<User>().ToListAsync();

// ✅ FASTER - Only needed fields
var justNames = await session.Query<User>()
    .Select(u => new { u.Id, u.Name })
    .ToListAsync();
```

### **4. Batch Queries**

```csharp
// ❌ SLOW - N+1 problem
foreach (var userId in userIds)
{
    var user = await session.LoadAsync<User>(userId);
    // Process user
}

// ✅ FAST - Single query
var users = await session.LoadManyAsync<User>(userIds);
foreach (var user in users)
{
    // Process user
}
```

---

## 🔍 PostgreSQL Query Insights

### **View Executed Queries**

Enable query logging to see translated SQL:

```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Log all SQL
    opts.Logger(new ConsoleMartenLogger());
});
```

### **Example Query Translation**

**C# LINQ:**
```csharp
var users = await session.Query<User>()
    .Where(u => u.Email.EndsWith("@gmail.com") && u.Name.StartsWith("J"))
    .OrderBy(u => u.Name)
    .Take(10)
    .ToListAsync();
```

**Generated SQL:**
```sql
SELECT data
FROM mt_doc_user
WHERE data->>'Email' LIKE '%@gmail.com'
  AND data->>'Name' LIKE 'J%'
ORDER BY data->>'Name'
LIMIT 10;
```

### **Analyze Query Performance**

```sql
-- In PostgreSQL
EXPLAIN ANALYZE
SELECT data FROM mt_doc_user
WHERE data->>'Email' LIKE '%@gmail.com';

-- Check if index is used
-- Look for "Index Scan" vs "Seq Scan"
```

---

## 🎓 Hands-On Exercises

### **Exercise 1: Complex Search**

Create a user search that handles multiple criteria:

```csharp
public static async Task<List<User>> SearchUsers(
    IDocumentStore store,
    string? nameFilter = null,
    string? emailDomain = null,
    int skip = 0,
    int take = 20)
{
    await using var session = store.LightweightSession();

    var query = session.Query<User>();

    if (!string.IsNullOrEmpty(nameFilter))
    {
        query = query.Where(u => u.Name.Contains(nameFilter));
    }

    if (!string.IsNullOrEmpty(emailDomain))
    {
        query = query.Where(u => u.Email.EndsWith(emailDomain));
    }

    return await query
        .OrderBy(u => u.Name)
        .Skip(skip)
        .Take(take)
        .ToListAsync();
}
```

### **Exercise 2: Compiled Query Library**

Build a reusable query library:

```csharp
public static class UserQueryLibrary
{
    public static readonly CompiledQuery<User> ById =
        (session, id) => session.Load<User>(id);

    public static readonly CompiledQuery<User> ByEmail =
        (session, email) => session.Query<User>()
            .FirstOrDefault(u => u.Email == email);

    public static readonly CompiledQuery<User> ByEmailDomain =
        (session, domain) => session.Query<User>()
            .Where(u => u.Email.EndsWith(domain))
            .OrderBy(u => u.Name)
            .ToList();

    public static readonly CompiledQuery<User> RecentUsers =
        (session, count) => session.Query<User>()
            .OrderByDescending(u => u.Id)
            .Take(count)
            .ToList();
}
```

### **Exercise 3: Full-Text Search Implementation**

Add comprehensive search:

```csharp
public record SearchResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public double Relevance { get; init; }
}

public static async Task<List<SearchResult>> FullTextUserSearch(
    IDocumentStore store,
    string searchTerm,
    int maxResults = 20)
{
    await using var session = store.LightweightSession();

    var results = await session.Query<User>()
        .Where(u => u.Search(searchTerm))
        .OrderByDescending(u => u.Rank(searchTerm))
        .Take(maxResults)
        .Select(u => new SearchResult
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Relevance = u.Rank(searchTerm)
        })
        .ToListAsync();

    return results;
}
```

---

## 💡 Best Practices

### **1. Use Appropriate Query Methods**

```csharp
// Direct load for known ID
var user = await session.LoadAsync<User>(id);

// First when expecting one result
var user = await session.Query<User>()
    .FirstOrDefaultAsync(u => u.Email == email);

// Single when result must be unique
var user = await session.Query<User>()
    .SingleOrDefaultAsync(u => u.Email == email);

// ToList for multiple results
var users = await session.Query<User>()
    .Where(u => u.Name.Contains("Smith"))
    .ToListAsync();
```

### **2. Avoid N+1 Queries**

```csharp
// ❌ BAD - N+1 problem
var orders = await session.Query<Order>().ToListAsync();
foreach (var order in orders)
{
    var user = await session.LoadAsync<User>(order.UserId); // N queries!
}

// ✅ GOOD - Batch load
var orders = await session.Query<Order>().ToListAsync();
var userIds = orders.Select(o => o.UserId).Distinct().ToList();
var users = await session.LoadManyAsync<User>(userIds); // 1 query
var userDict = users.ToDictionary(u => u.Id);

foreach (var order in orders)
{
    var user = userDict[order.UserId];
}
```

### **3. Limit Result Sets**

```csharp
// ❌ DANGEROUS - Could return millions
var allUsers = await session.Query<User>().ToListAsync();

// ✅ SAFE - Always limit
var recentUsers = await session.Query<User>()
    .OrderByDescending(u => u.Id)
    .Take(100)
    .ToListAsync();
```

---

## 📊 Query Performance Summary

| Pattern | Performance | Use When |
|---------|-------------|----------|
| `LoadAsync(id)` | ⚡⚡⚡ Fastest | Known ID |
| `LoadManyAsync(ids)` | ⚡⚡⚡ Fast | Multiple known IDs |
| Compiled Query | ⚡⚡ Very Fast | Repeated queries |
| LINQ Query (indexed) | ⚡⚡ Fast | Indexed fields |
| LINQ Query (no index) | ⚡ Slow | Rare queries only |
| Full-Text Search | ⚡⚡ Fast | Text search with FTS index |

---

## 🎓 What You've Learned

Excellent work! You now know:

- ✅ **LINQ queries** - Filtering, ordering, paging, aggregations
- ✅ **Nested queries** - Query complex object graphs
- ✅ **Compiled queries** - Optimize hot paths
- ✅ **Full-text search** - Leverage PostgreSQL FTS
- ✅ **Query optimization** - Indexes, projections, batching
- ✅ **Best practices** - Avoid N+1, limit results, use appropriate methods

---

## 🚀 Next Steps

Ready to dive deeper? In **[Chapter 03 - Identity & Schema](TUTORIAL-03-Identity-Schema.md)**, we'll explore:

- ID generation strategies
- Custom indexes and unique constraints
- Schema customization
- Foreign key relationships
- Soft deletes

**Continue to [Chapter 03 - Identity & Schema](TUTORIAL-03-Identity-Schema.md) →**

---

**Questions?** Review this chapter or go back to [Chapter 01 - Basics](TUTORIAL-01-Basics.md).
