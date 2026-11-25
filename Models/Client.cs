using SQLite;

namespace UltimateManager.Models;

[Table("Clients")]
public sealed class Client
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid WorkspaceId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    [Ignore]
    public string FullName => $"{FirstName} {LastName}".Trim();

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Address { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
