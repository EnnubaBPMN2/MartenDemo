# 🚀 Marten Tutorial - Introduction

Welcome to this comprehensive, hands-on Marten tutorial! This guide will take you from the basics of document storage to advanced event sourcing concepts, all using PostgreSQL as the underlying database.

---

## 📘 What is Marten?

**Marten** is a powerful .NET library that transforms PostgreSQL into a robust document database and event store. Created by Jeremy D. Miller and the JasperFx team, Marten provides:

### **Core Capabilities:**

1. **Document Database**
   - Store .NET objects as JSON documents in PostgreSQL
   - Query using LINQ with full type safety
   - Leverage PostgreSQL's JSONB and GIN indexes for performance

2. **Event Store**
   - Complete event sourcing implementation
   - Event streams with immutable event history
   - Built-in support for aggregates and projections

3. **CQRS Support**
   - Command/Query separation
   - Live and async projections
   - Multiple read model strategies

4. **Hybrid Approach**
   - Combine document storage with relational queries
   - Use both document and event sourcing in the same application
   - Full ACID compliance through PostgreSQL

### **Why Marten?**

- **PostgreSQL-Native**: No separate database - everything runs on PostgreSQL
- **Type-Safe**: Strongly-typed C# with LINQ queries
- **Performance**: Leverages PostgreSQL's advanced JSON capabilities
- **Production-Ready**: Battle-tested in enterprise applications
- **Open Source**: Active community and development

---

## 🎯 Tutorial Objectives

By the end of this tutorial series, you will:

- ✅ Understand Marten's document database capabilities
- ✅ Master querying patterns and performance optimization
- ✅ Learn identity management and schema configuration
- ✅ Understand session types and unit of work patterns
- ✅ Implement optimistic concurrency control
- ✅ Build event-sourced aggregates
- ✅ Create projections and read models
- ✅ Apply advanced patterns and best practices

---

## 👥 Target Audience

This tutorial is designed for:

- **Proficient C# developers** who want to learn Marten
- Developers with **decent PostgreSQL knowledge**
- Those interested in **document databases** and **event sourcing**
- Teams evaluating Marten for production use

### **Prerequisites:**

**Required:**
- C# 12+ and .NET 8.0+ knowledge
- Understanding of async/await patterns
- Basic PostgreSQL familiarity
- LINQ query syntax

**Helpful (but not required):**
- Event sourcing concepts
- CQRS pattern
- Domain-driven design (DDD)

---

## 🏗️ Repository Structure

```
MartenDemo/
├── Program.cs                          # Main application with chapter examples
├── appsettings.json                    # Configuration file
├── Models/                             # Domain models
│   ├── User.cs                         # Simple document example
│   ├── Product.cs                      # E-commerce examples
│   └── Order.cs                        # Complex document example
├── EventSourcing/                      # Event sourcing examples
│   ├── Events/                         # Event definitions
│   ├── Aggregates/                     # Aggregate roots
│   └── Projections/                    # Read models
├── Helpers/                            # Utility classes
│   ├── DataSeeder.cs                   # Sample data generation
│   └── DatabaseReset.cs                # Clean slate for experiments
└── TUTORIAL-XX-*.md                    # Tutorial chapters
```

---

## 🛠️ Setup Instructions

### **1. Prerequisites Installation**

**PostgreSQL 14+** (Recommended: 16+)
```bash
# Verify PostgreSQL is running
psql --version

# Create the demo database
psql -U postgres -c "CREATE DATABASE marten_demo;"
```

**.NET 8.0 or higher**
```bash
# Verify .NET installation
dotnet --version
```

### **2. Clone and Build**

```bash
# Clone the repository
git clone <your-repo-url>
cd MartenDemo

# Restore packages
dotnet restore

# Build the project
dotnet build
```

### **3. Configure Connection String**

**Option A: appsettings.json** (default)
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

**Option B: Environment Variable** (recommended for production)
```bash
# Windows PowerShell
$env:CONN="Host=localhost;Port=5432;Database=marten_demo;Username=postgres;Password=yourpassword"

# Linux/Mac
export CONN="Host=localhost;Port=5432;Database=marten_demo;Username=postgres;Password=yourpassword"
```

### **4. Run the Demo**

```bash
dotnet run
```

---

## 📚 Tutorial Structure

This tutorial is organized into progressive chapters:

| Chapter | Topic | Concepts |
|---------|-------|----------|
| **[01 - Basics](TUTORIAL-01-Basics.md)** | Document Database Fundamentals | DocumentStore, Sessions, CRUD |
| **[02 - Querying](TUTORIAL-02-Querying.md)** | Advanced Queries | LINQ, Compiled Queries, Full-Text Search |
| **[03 - Identity & Schema](TUTORIAL-03-Identity-Schema.md)** | Document Management | ID Strategies, Indexes, Constraints |
| **[04 - Sessions](TUTORIAL-04-Sessions.md)** | Unit of Work Patterns | Lightweight, Identity Map, Dirty Tracking |
| **[05 - Concurrency](TUTORIAL-05-Concurrency.md)** | Conflict Resolution | Optimistic Concurrency, Versioning |
| **[06 - Event Sourcing](TUTORIAL-06-EventSourcing.md)** | Event-Driven Architecture | Events, Streams, Aggregates |
| **[07 - Projections](TUTORIAL-07-Projections.md)** | Read Models | Live, Inline, Async Projections |
| **[08 - Advanced](TUTORIAL-08-Advanced.md)** | Production Patterns | Multi-tenancy, Patching, Best Practices |

---

## 🎓 How to Use This Tutorial

### **Learning Path:**

1. **Sequential Learning** (Recommended)
   - Follow chapters in order (01 → 08)
   - Each chapter builds on previous concepts
   - Run code examples as you progress

2. **Topic-Based Learning**
   - Jump to specific chapters for focused learning
   - Use the index to find relevant topics
   - Reference back to fundamentals as needed

### **Code Examples:**

Each tutorial chapter includes:
- **Conceptual Explanation** - What and why
- **Code Walkthrough** - How it works
- **Hands-On Exercise** - Build it yourself
- **PostgreSQL Insights** - What happens under the hood
- **Best Practices** - Production-ready patterns

### **Running Examples:**

The `Program.cs` file contains a menu system:
```bash
dotnet run
```

Select a chapter to run its examples interactively.

---

## 🔍 PostgreSQL Under the Hood

One unique aspect of this tutorial is **PostgreSQL transparency**. We'll show you:

- What tables Marten creates
- How documents are stored (JSONB format)
- Indexes and performance implications
- Event store schema design
- Query execution plans

**Example: Viewing stored documents**
```sql
-- See how Marten stores your User documents
SELECT id, data FROM public.mt_doc_user;

-- View the JSONB structure
SELECT data->>'Name' as name,
       data->>'Email' as email
FROM public.mt_doc_user;
```

---

## 💡 Tips for Success

1. **Experiment Freely** - Use the database reset functionality to start fresh
2. **Read PostgreSQL Output** - Understanding the database helps debug issues
3. **Try Variations** - Modify examples to test your understanding
4. **Ask Questions** - Clarify concepts as you go
5. **Build Something Real** - Apply patterns to your own domain

---

## 🆘 Getting Help

**Official Resources:**
- [Marten Documentation](https://martendb.io/)
- [GitHub Repository](https://github.com/JasperFx/marten)
- [JasperFx Blog](https://jeremydmiller.com/)

**Common Issues:**
- **Connection errors**: Verify PostgreSQL is running and connection string is correct
- **Schema errors**: Ensure `AutoCreateSchemaObjects` is set to `All` for development
- **Performance**: Check indexes and query patterns (covered in Chapter 03)

---

## ✨ What Makes This Tutorial Different?

1. **Real Code First** - Learn from a working application, not toy examples
2. **Progressive Enhancement** - Start simple, add complexity gradually
3. **PostgreSQL Insights** - See both C# and database perspectives
4. **Production Focus** - Learn patterns used in real applications
5. **Event Sourcing Depth** - Comprehensive coverage of ES concepts

---

## 🚦 Getting Started

Ready to dive in? Start with **[Chapter 01 - Basics](TUTORIAL-01-Basics.md)** to understand Marten's document database fundamentals.

**Quick Start Checklist:**
- [ ] PostgreSQL 14+ installed and running
- [ ] .NET 8.0+ installed
- [ ] Repository cloned and built
- [ ] Connection string configured
- [ ] `dotnet run` executes successfully

---

## 📝 Tutorial Conventions

Throughout these tutorials:

- `Program.cs:36` - References line 36 in Program.cs
- **Bold** - Important concepts
- `Code blocks` - C# code or commands
- 💡 - Tips and best practices
- ⚠️ - Common pitfalls
- 🔍 - PostgreSQL insights

---

**Let's begin! Continue to [Chapter 01 - Basics](TUTORIAL-01-Basics.md) →**
