namespace UltimateManager.Services;

public interface IPlaceSearchProvider
{
    Task<IReadOnlyList<PlaceSuggestion>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<LocationResult?> GetLocationAsync(
        string placeId,
        CancellationToken cancellationToken = default);

    Task<string?> ResolveAddressAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);
}
