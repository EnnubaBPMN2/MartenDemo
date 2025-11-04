using Marten;
using MartenDemo.EventSourcing.Projections;
using MartenDemo.Models;

namespace MartenDemo.Helpers;

public static class DatabaseReset
{
    /// <summary>
    ///     Resets all document data (keeps schema)
    /// </summary>
    public static async Task ResetDocumentsAsync(IDocumentStore store)
    {
        await using var session = store.LightweightSession();

        // Delete all documents
        session.DeleteWhere<User>(_ => true);
        session.DeleteWhere<Product>(_ => true);
        session.DeleteWhere<Order>(_ => true);
        session.DeleteWhere<Contact>(_ => true);

        // Delete projections
        session.DeleteWhere<AccountBalance>(_ => true);
        session.DeleteWhere<TransactionHistory>(_ => true);

        await session.SaveChangesAsync();

        Console.WriteLine("✅ All documents deleted");
    }

    /// <summary>
    ///     Resets all events
    /// </summary>
    public static async Task ResetEventsAsync(IDocumentStore store)
    {
        // Clean up events using Marten's advanced API
        await store.Advanced.Clean.DeleteAllEventDataAsync();

        Console.WriteLine("✅ All events deleted");
    }

    /// <summary>
    ///     Complete reset - documents and events
    /// </summary>
    public static async Task CompleteResetAsync(IDocumentStore store)
    {
        Console.WriteLine("🔄 Performing complete database reset...");

        await ResetEventsAsync(store);
        await ResetDocumentsAsync(store);

        Console.WriteLine("✅ Database reset complete");
    }

    /// <summary>
    ///     Drop and recreate all schema objects
    /// </summary>
    public static async Task RecreateSchemaAsync(IDocumentStore store)
    {
        Console.WriteLine("🔄 Dropping and recreating schema...");

        // Drop all Marten tables
        await store.Advanced.Clean.CompletelyRemoveAllAsync();

        // Recreate schema
        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        Console.WriteLine("✅ Schema recreated");
    }
}