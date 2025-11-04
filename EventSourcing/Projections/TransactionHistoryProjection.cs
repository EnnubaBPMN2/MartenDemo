using Marten.Events.Aggregation;
using MartenDemo.EventSourcing.Events;

namespace MartenDemo.EventSourcing.Projections;

// Read model for transaction history
public class TransactionHistory
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public List<Transaction> Transactions { get; set; } = new();
}

public record Transaction(string Type, decimal Amount, string Description, DateTime When);

// Projection definition using SingleStreamProjection
public class TransactionHistoryProjection : SingleStreamProjection<TransactionHistory, Guid>
{
    public TransactionHistory Create(AccountOpened evt)
    {
        return new TransactionHistory
        {
            Id = evt.AccountId,
            AccountNumber = evt.AccountNumber,
            Transactions = new List<Transaction>
            {
                new("Opened", evt.InitialBalance, "Account opened with initial deposit", evt.OpenedAt)
            }
        };
    }

    public void Apply(MoneyDeposited evt, TransactionHistory view)
    {
        view.Transactions.Add(new Transaction("Deposit", evt.Amount, evt.Description, evt.DepositedAt));
    }

    public void Apply(MoneyWithdrawn evt, TransactionHistory view)
    {
        view.Transactions.Add(new Transaction("Withdrawal", -evt.Amount, evt.Description, evt.WithdrawnAt));
    }

    public void Apply(AccountClosed evt, TransactionHistory view)
    {
        view.Transactions.Add(new Transaction("Closed", 0, evt.Reason, evt.ClosedAt));
    }
}
