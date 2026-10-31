namespace UltimateManager.Services;

public interface IMapProvider
{
    Task<LocationResult?> PickAsync(
        LocationResult? initialLocation = null,
        CancellationToken cancellationToken = default);
}
