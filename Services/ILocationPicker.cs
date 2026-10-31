namespace UltimateManager.Services;

public interface ILocationPicker
{
    Task<LocationCoordinate?> PickAsync(
        double? latitude = null,
        double? longitude = null,
        CancellationToken cancellationToken = default);
}