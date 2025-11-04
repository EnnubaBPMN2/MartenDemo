# Fix: Corrected Marten 8.x Event Sourcing Projection Implementation

## ?? Summary

This PR fixes critical compilation errors in the event sourcing projection classes caused by using incorrect base classes that don't exist in Marten 8.13.3.

## ?? Problem

The codebase was using `ViewProjection<TDoc, TId>` as the base class for projections, which **does not exist** in Marten 8.x. This caused build failures with error:

```
CS0246: The type or namespace name 'ViewProjection<,>' could not be found
```

## ? Solution

### 1. **Fixed Projection Base Classes**

**Changed from:**
```csharp
public class AccountBalanceProjection : ViewProjection<AccountBalance, Guid>
{
    public AccountBalanceProjection()
    {
        Identity<AccountOpened>(x => x.AccountId);
        Identity<MoneyDeposited>(x => x.AccountId);
        // ... constructor-based configuration
    }
}
```

**Changed to:**
```csharp
public class AccountBalanceProjection : SingleStreamProjection<AccountBalance, Guid>
{
    public AccountBalance Create(AccountOpened evt)
    {
        return new AccountBalance
        {
            Id = evt.AccountId,
            AccountNumber = evt.AccountNumber,
            // ... property initialization
        };
    }

    public void Apply(MoneyDeposited evt, AccountBalance view)
    {
        view.Balance += evt.Amount;
        view.LastModified = evt.DepositedAt;
    }
}
```

**Key Changes:**
- ? Replaced `ViewProjection<TDoc, TId>` with `SingleStreamProjection<TDoc, TId>`
- ? Changed from constructor-based configuration to method-based projections
- ? Added `Create()` method for stream initialization
- ? Added `Apply()` methods for event handling
- ? Updated namespace from `Marten.Events.Projections` to `Marten.Events.Aggregation`

### 2. **Fixed Projection Registration**

**Changed from:**
```csharp
opts.Projections.Add<AccountBalanceProjection>(ProjectionLifecycle.Inline);
opts.Projections.Add<TransactionHistoryProjection>(ProjectionLifecycle.Inline);
```

**Changed to:**
```csharp
opts.Projections.Add<AccountBalanceProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
opts.Projections.Add<TransactionHistoryProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
```

**Key Finding:** `ProjectionLifecycle` is in the `JasperFx.Events.Projections` namespace, not `Marten.Events.Projections`.

### 3. **Fixed Marten 8.x API Breaking Changes**

#### IdentitySession()
```csharp
// Updated for Marten 8.x
await using (var session = _store!.IdentitySession())
```

#### Storage Property
```csharp
// Old (Marten 7.x)
await store.Schema.ApplyAllConfiguredChangesToDatabaseAsync();

// New (Marten 8.x)
await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
```

#### Concurrency Handling
Updated concurrency demo to use `VersionFor()` and proper exception handling for Marten 8.x.

### 4. **Fixed Type Conversions**

```csharp
// Fixed double to decimal implicit conversion error
Price = (decimal)Math.Round(Random.Shared.NextDouble() * 500 + 10, 2)
```

## ?? Files Changed

1. **EventSourcing/Projections/AccountBalanceProjection.cs**
   - Changed base class from `ViewProjection` to `SingleStreamProjection<TDoc, TId>`
   - Refactored to use `Create()` and `Apply()` methods
   - Updated namespace to `Marten.Events.Aggregation`

2. **EventSourcing/Projections/TransactionHistoryProjection.cs**
   - Changed base class from `ViewProjection` to `SingleStreamProjection<TDoc, TId>`
   - Refactored to use `Create()` and `Apply()` methods
   - Updated namespace to `Marten.Events.Aggregation`

3. **Program.cs**
   - Fixed projection registration to use `JasperFx.Events.Projections.ProjectionLifecycle.Inline`
   - Updated `OpenSession()` to `IdentitySession()`
   - Fixed concurrency checking to use `VersionFor()`
   - Removed obsolete API calls

4. **Helpers/DatabaseReset.cs**
   - Changed `store.Schema` to `store.Storage`

5. **Helpers/DataSeeder.cs**
   - Fixed double to decimal conversion with explicit cast

## ? Why These Changes?

### Marten 8.x Breaking Changes

Marten 8.0 introduced significant breaking changes to the projection API:

1. **Removed `ViewProjection`**: This class doesn't exist in Marten 8.x
2. **Introduced `SingleStreamProjection<TDoc, TId>`**: The new standard for single-stream projections with two type parameters
3. **Method-based over Constructor-based**: Projections now use methods like `Create()` and `Apply()` instead of constructor configuration
4. **ProjectionLifecycle in JasperFx**: The enum moved to `JasperFx.Events.Projections` namespace

## ?? Testing

- [x] Solution builds successfully
- [x] All compilation errors resolved
- [x] Application runs successfully
- [x] Menu system displays correctly
- [x] Projection classes use correct Marten 8.x API

## ?? References

- [Marten 8.x Documentation](https://martendb.io/)
- [Marten GitHub - Breaking Changes](https://github.com/JasperFx/marten)
- [SingleStreamProjection Documentation](https://martendb.io/events/projections/aggregate-projections.html)

## ?? What We Learned

1. `ViewProjection<TDoc, TId>` **does not exist** in Marten 8.x
2. Use `SingleStreamProjection<TDoc, TId>` from `Marten.Events.Aggregation`
3. Projections require **two type parameters**: document type and ID type
4. Marten 8.x prefers method-based projection configuration
5. **`ProjectionLifecycle` is in JasperFx**, not Marten namespace
6. Always check the official documentation when upgrading major versions

## ?? Notes

This was likely caused by referencing outdated Marten documentation or examples from pre-8.0 versions. All code now conforms to Marten 8.13.3 API standards.

## ?? Next Steps

- Test event sourcing examples in Chapter 06 and 07
- Verify projections work correctly with actual data
- Consider updating tutorial documentation to reflect Marten 8.x changes

---

## ?? Commit Message Suggestion

```
fix: Correct Marten 8.x projection implementation

- Replace ViewProjection with SingleStreamProjection<TDoc, TId>
- Update projection registration to use JasperFx.Events.Projections.ProjectionLifecycle
- Fix Marten 8.x breaking changes (IdentitySession, Storage, etc.)
- Add explicit decimal conversion in DataSeeder
- Application now runs successfully

Fixes #<issue-number>
```

---

**?? Important:** Before merging, ensure that:
1. PostgreSQL is running and accessible
2. Connection string is properly configured
3. Run integration tests if available
4. Test event sourcing functionality manually (Chapter 7)

