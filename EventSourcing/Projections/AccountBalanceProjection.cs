using Marten.Events.Aggregation;
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

// Projection definition using SingleStreamProjection
public class AccountBalanceProjection : SingleStreamProjection<AccountBalance, Guid>
{
    public AccountBalance Create(AccountOpened evt)
    {
        return new AccountBalance
        {
            Id = evt.AccountId,
            AccountNumber = evt.AccountNumber,
            OwnerName = evt.OwnerName,
            Balance = evt.InitialBalance,
            LastModified = evt.OpenedAt,
            IsClosed = false
        };
    }

    public void Apply(MoneyDeposited evt, AccountBalance view)
    {
        view.Balance += evt.Amount;
        view.LastModified = evt.DepositedAt;
    }

    public void Apply(MoneyWithdrawn evt, AccountBalance view)
    {
        view.Balance -= evt.Amount;
        view.LastModified = evt.WithdrawnAt;
    }

    public void Apply(AccountClosed evt, AccountBalance view)
    {
        view.IsClosed = true;
        view.LastModified = evt.ClosedAt;
    }
}