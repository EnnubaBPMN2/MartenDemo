namespace MartenDemo.Models;

public record Product
{
    public Guid Id { get; init; }
    public required string SKU { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
    public List<string> Tags { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; init; }
}
