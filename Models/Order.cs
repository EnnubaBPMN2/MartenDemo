namespace MartenDemo.Models;

public record Order
{
    public Guid Id { get; init; }
    public required string OrderNumber { get; init; }
    public Guid UserId { get; init; }
    public List<OrderItem> Items { get; init; } = new();
    public decimal Total { get; init; }
    public OrderStatus Status { get; init; } = OrderStatus.Pending;
    public DateTime OrderedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ShippedAt { get; init; }
}

public record OrderItem
{
    public Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal => Quantity * UnitPrice;
}

public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}
