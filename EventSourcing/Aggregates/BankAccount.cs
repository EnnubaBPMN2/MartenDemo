using MartenDemo.EventSourcing.Events;

namespace MartenDemo.EventSourcing.Aggregates;

public class BankAccount
{
    // State
    public Guid Id { get; private set; }
    public string AccountNumber { get; private set; } = "";
    public string OwnerName { get; private set; } = "";
    public decimal Balance { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastModified { get; private set; }

    // Event handlers - Apply methods rebuild state from events
    public void Apply(AccountOpened e)
    {
        Id = e.AccountId;
        AccountNumber = e.AccountNumber;
        OwnerName = e.OwnerName;
        Balance = e.InitialBalance;
        IsClosed = false;
        CreatedAt = e.OpenedAt;
        LastModified = e.OpenedAt;
    }

    public void Apply(MoneyDeposited e)
    {
        Balance += e.Amount;
        LastModified = e.DepositedAt;
    }

    public void Apply(MoneyWithdrawn e)
    {
        Balance -= e.Amount;
        LastModified = e.WithdrawnAt;
    }

    public void Apply(AccountClosed e)
    {
        IsClosed = true;
        LastModified = e.ClosedAt;
    }

    // Business logic validation (optional)
    public bool CanWithdraw(decimal amount)
    {
        return !IsClosed && Balance >= amount;
    }

    public bool CanDeposit()
    {
        return !IsClosed;
    }
}
