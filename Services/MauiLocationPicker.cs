using UltimateManager.Views;

namespace UltimateManager.Services;

public sealed class GoogleMapProvider : IMapProvider
{
    private readonly IPlaceSearchProvider _placeSearchProvider;

    public GoogleMapProvider(IPlaceSearchProvider placeSearchProvider)
    {
        _placeSearchProvider = placeSearchProvider;
    }

    public async Task<LocationResult?> PickAsync(
        LocationResult? initialLocation = null,
        CancellationToken cancellationToken = default)
    {
        var page = new LocationPickerPage(initialLocation, _placeSearchProvider, cancellationToken);
        await Shell.Current.Navigation.PushModalAsync(page);
        LocationResult? result;
        try
        {
            result = await page.Result;
        }
        catch (OperationCanceledException)
        {
            result = null;
        }

        if (Shell.Current.Navigation.ModalStack.Contains(page))
        {
            await Shell.Current.Navigation.PopModalAsync();
        }

        return result;
    }
}