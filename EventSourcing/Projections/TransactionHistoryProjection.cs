using Marten.Events.Projections;
using MartenDemo.EventSourcing.Events;

namespace MartenDemo.EventSourcing.Projections;

// Read model for transaction history
public class TransactionHistory
{
    public Guid Id { get; set; } // Account ID
    public string AccountNumber { get; set; } = "";
    public List<Transaction> Transactions { get; set; } = new();
}

public record Transaction(string Type, decimal Amount, string Description, DateTime When);

// Projection definition
public class TransactionHistoryProjection : SingleStreamProjection<TransactionHistory>
{
    public TransactionHistory Create(AccountOpened e)
    {
        return new TransactionHistory
        {
            Id = e.AccountId,
            AccountNumber = e.AccountNumber,
            Transactions = new List<Transaction>
            {
                new("Opened", e.InitialBalance, $"Account opened with initial deposit", e.OpenedAt)
            }
        };
    }

    public void Apply(MoneyDeposited e, TransactionHistory view)
    {
        view.Transactions.Add(new Transaction("Deposit", e.Amount, e.Description, e.DepositedAt));
    }

    public void Apply(MoneyWithdrawn e, TransactionHistory view)
    {
        view.Transactions.Add(new Transaction("Withdrawal", -e.Amount, e.Description, e.WithdrawnAt));
    }

    public void Apply(AccountClosed e, TransactionHistory view)
    {
        view.Transactions.Add(new Transaction("Closed", 0, e.Reason, e.ClosedAt));
    }
}
