namespace MartenDemo.Models;

public record Address(string Street, string City, string State, string ZipCode, string Country);

public record Contact
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public List<string> PhoneNumbers { get; init; } = new();
    public Address? HomeAddress { get; init; }
    public Address? WorkAddress { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
