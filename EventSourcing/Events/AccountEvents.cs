namespace MartenDemo.EventSourcing.Events;

// Account lifecycle events
public record AccountOpened(
    Guid AccountId,
    string AccountNumber,
    string OwnerName,
    decimal InitialBalance,
    DateTime OpenedAt
);

public record MoneyDeposited(
    Guid AccountId,
    decimal Amount,
    string Description,
    DateTime DepositedAt
);

public record MoneyWithdrawn(
    Guid AccountId,
    decimal Amount,
    string Description,
    DateTime WithdrawnAt
);

public record AccountClosed(
    Guid AccountId,
    decimal FinalBalance,
    string Reason,
    DateTime ClosedAt
);