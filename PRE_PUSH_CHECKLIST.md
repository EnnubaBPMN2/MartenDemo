# ? Pre-Push Checklist

Before pushing these changes to GitHub, go through this checklist:

## ?? Code Review

- [ ] Review all changed files to understand what was fixed
- [ ] Check that `AccountBalanceProjection.cs` uses `SingleStreamProjection<AccountBalance, Guid>`
- [ ] Check that `TransactionHistoryProjection.cs` uses `SingleStreamProjection<TransactionHistory, Guid>`
- [ ] Verify Program.cs projection registration uses `Snapshot<T>()`
- [ ] Confirm all using statements are correct

## ?? Testing

- [ ] Run `dotnet build` - confirms it builds ? (DONE)
- [ ] Run `dotnet run` - test the application starts
- [ ] Navigate to Chapter 7 (Projections) and test it works
- [ ] Verify events are being projected to `AccountBalance` and `TransactionHistory`
- [ ] Check database to confirm projection documents are created

## ?? Documentation

- [ ] Read `PR_DESCRIPTION.md` to understand all changes
- [ ] Read `COWORKER_SUMMARY.md` for a quick explanation
- [ ] Update any internal docs if needed

## ?? Git Workflow

- [ ] Stage all changed files:
  ```bash
  git add EventSourcing/Projections/AccountBalanceProjection.cs
  git add EventSourcing/Projections/TransactionHistoryProjection.cs
  git add Program.cs
  git add Helpers/DatabaseReset.cs
  git add Helpers/DataSeeder.cs
  ```

- [ ] Commit with a clear message:
  ```bash
  git commit -m "fix: Correct Marten 8.x projection implementation

  - Replace ViewProjection with SingleStreamProjection<TDoc, TId>
  - Update projection registration to use Snapshot API  
  - Fix Marten 8.x breaking changes (IdentitySession, Storage, etc.)
  - Add explicit decimal conversion in DataSeeder
  
  Resolves build errors caused by using incorrect Marten API"
  ```

- [ ] Push to your branch:
  ```bash
  git push origin claude/marten-tutorial-creation-011CUoLqCVjkM8vDB3M4VG3u
  ```

- [ ] Create Pull Request on GitHub with:
  - Title: "Fix: Correct Marten 8.x projection implementation"
  - Description: Copy content from `PR_DESCRIPTION.md`
  - Request review from your coworker

## ?? Communication

- [ ] Send `COWORKER_SUMMARY.md` to your coworker
- [ ] Explain this was a Marten version issue (not their fault!)
- [ ] Share link to Marten 8.x docs for reference
- [ ] Offer to pair program if they want to understand the changes

## ?? Learning Points to Share

Explain to your coworker:
1. **Why it broke**: `ViewProjection` doesn't exist in Marten 8.x
2. **What the fix was**: Use `SingleStreamProjection<TDoc, TId>` instead  
3. **How to avoid**: Always check official docs for your package version
4. **Marten 8.x changes**: Several breaking changes in the API

## ?? Resources to Share

- Marten Docs: https://martendb.io/
- Projections Guide: https://martendb.io/events/projections/
- GitHub Issues: https://github.com/JasperFx/marten/issues (for questions)

## ? Optional Improvements

Consider these follow-up improvements:
- [ ] Add XML documentation comments to projection classes
- [ ] Add unit tests for projections
- [ ] Update tutorial markdown files if needed
- [ ] Add logging to projection Apply methods

---

## ?? Summary of Changes

**Files Changed**: 5
**Lines Changed**: ~150
**Build Status**: ? Success
**Breaking Changes Fixed**: 8

**Key Fixes**:
1. ? Projection base class (ViewProjection ? SingleStreamProjection)
2. ? Projection registration (Add ? Snapshot)
3. ? Session creation (OpenSession ? IdentitySession)
4. ? Schema access (Schema ? Storage)
5. ? Type conversion (double ? decimal)

---

**You're ready to push! ??**

Once you've completed this checklist, your changes will be ready for review and merge.
