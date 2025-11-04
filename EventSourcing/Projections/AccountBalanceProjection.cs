using Marten.Events.Projections;
using MartenDemo.EventSourcing.Events;

namespace MartenDemo.EventSourcing.Projections;

// Read model for account balance
public class AccountBalance
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public decimal Balance { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsClosed { get; set; }
}

// Projection definition - updates AccountBalance document from events
public class AccountBalanceProjection : SingleStreamProjection<AccountBalance>
{
    // Create document when stream starts
    public AccountBalance Create(AccountOpened e)
    {
        return new AccountBalance
        {
            Id = e.AccountId,
            AccountNumber = e.AccountNumber,
            OwnerName = e.OwnerName,
            Balance = e.InitialBalance,
            LastModified = e.OpenedAt,
            IsClosed = false
        };
    }

    // Update document when events occur
    public void Apply(MoneyDeposited e, AccountBalance view)
    {
        view.Balance += e.Amount;
        view.LastModified = e.DepositedAt;
    }

    public void Apply(MoneyWithdrawn e, AccountBalance view)
    {
        view.Balance -= e.Amount;
        view.LastModified = e.WithdrawnAt;
    }

    public void Apply(AccountClosed e, AccountBalance view)
    {
        view.IsClosed = true;
        view.LastModified = e.ClosedAt;
    }
}
