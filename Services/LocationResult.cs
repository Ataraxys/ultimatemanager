namespace UltimateManager.Services;

public sealed record LocationResult(
    double Latitude,
    double Longitude,
    string Address,
    string? PlaceId = null);
