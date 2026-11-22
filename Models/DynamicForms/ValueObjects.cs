namespace UltimateManager.Models.DynamicForms;

public sealed class AddressValue
{
    public string Street { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public GeoPointValue? Location { get; set; }

    public string ToDisplayString() => string.Join(", ", new[]
    {
        $"{Number} {Street}".Trim(),
        PostalCode,
        City,
        Country
    }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

public sealed class PhoneNumberValue
{
    public string CountryCode { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public sealed class GeoPointValue
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class CommentValue
{
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DocumentValue
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
}
