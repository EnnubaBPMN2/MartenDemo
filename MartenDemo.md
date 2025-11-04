# 🧩 Marten Demo — Learning Notes

## 📘 Project Overview

This demo is a minimal C# console application that demonstrates how to use **Marten** as a **document database** layer over **PostgreSQL**.  
Marten stores .NET objects as JSON documents inside PostgreSQL tables — allowing you to work with **documents, queries, and event sourcing** while keeping all data in a relational database.

---

## 🏗️ Project Structure

**Project name:** `MartenDemo`  
**Framework:** `.NET 10.0 RC2`  
**Database:** PostgreSQL 16+  
**Main file:** `Program.cs`  
**Configuration file:** `appsettings.json`

---

## ⚙️ Current Code Summary

```csharp
using JasperFx;
using Marten;
using Microsoft.Extensions.Configuration;

public record User
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
}

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false, true)
            .AddEnvironmentVariables();

        var config = builder.Build();

        var connection = Environment.GetEnvironmentVariable("CONN")
                         ?? config.GetConnectionString("Postgres");

        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("No PostgreSQL connection string found.");
        }

        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(connection);
            opts.AutoCreateSchemaObjects = Enum.TryParse<AutoCreate>(
                config["Marten:AutoCreateSchemaObjects"], out var autoCreate)
                ? autoCreate
                : AutoCreate.None;
        });

        await using var session = store.LightweightSession();

        var user = new User { Id = Guid.NewGuid(), Name = "Hermann", Email = "h@ennuba.com" };
        session.Store(user);
        await session.SaveChangesAsync();

        var dbUser = session.Query<User>().FirstOrDefault(u => u.Email == "h@ennuba.com");
        Console.WriteLine($"Usuario guardado: {dbUser?.Name} - {dbUser?.Email}");
    }
}
```
