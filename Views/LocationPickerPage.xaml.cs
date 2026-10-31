using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using UltimateManager.Services;
using MauiMap = Microsoft.Maui.Controls.Maps.Map;

namespace UltimateManager.Views;

public partial class LocationPickerPage : ContentPage
{
    private readonly IPlaceSearchProvider _placeSearchProvider;
    private readonly TaskCompletionSource<LocationResult?> _result = new();
    private readonly CancellationTokenRegistration _cancellationRegistration;
    private readonly ObservableCollection<PlaceSuggestion> _suggestions = [];
    private readonly MauiMap _locationMap;
    private CancellationTokenSource? _searchCancellation;
    private Pin? _selectedPin;
    private LocationResult? _selectedLocation;
    private bool _hasDisplayedInitialRegion;

    public LocationPickerPage(
        LocationResult? initialLocation,
        IPlaceSearchProvider placeSearchProvider,
        CancellationToken cancellationToken)
    {
        InitializeComponent();
        _placeSearchProvider = placeSearchProvider;
        _locationMap = new MauiMap
        {
            MapType = MapType.Street,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _locationMap.MapClicked += OnMapClicked;
        MapHost.Content = _locationMap;
        Suggestions.ItemsSource = _suggestions;

        if (initialLocation is not null)
        {
            SetSelectedLocation(initialLocation, moveMap: false);
        }

        MoveMapToInitialLocation(initialLocation);

        _cancellationRegistration = cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(Cancel));
    }

    public Task<LocationResult?> Result => _result.Task;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_hasDisplayedInitialRegion)
        {
            MoveMapToInitialLocation(_selectedLocation);
        }
    }

    private void MoveMapToInitialLocation(LocationResult? location)
    {
        var center = location is null
            ? new Location(48.8566, 2.3522)
            : new Location(location.Latitude, location.Longitude);
        _locationMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            center,
            Distance.FromKilometers(location is null ? 5 : 1)));
        _hasDisplayedInitialRegion = true;
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        var cancellationToken = _searchCancellation.Token;

        try
        {
            if (string.IsNullOrWhiteSpace(e.NewTextValue) || e.NewTextValue.Trim().Length < 3)
            {
                _suggestions.Clear();
                Suggestions.IsVisible = false;
                return;
            }

            await Task.Delay(250, cancellationToken);
            var results = await _placeSearchProvider.SearchAsync(e.NewTextValue, cancellationToken);
            _suggestions.Clear();
            foreach (var suggestion in results)
            {
                _suggestions.Add(suggestion);
            }

            Suggestions.IsVisible = _suggestions.Count > 0;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            _suggestions.Clear();
            Suggestions.IsVisible = false;
        }
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e)
    {
        Suggestions.IsVisible = _suggestions.Count > 0;
    }

    private async void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not PlaceSuggestion suggestion)
        {
            return;
        }

        Suggestions.SelectedItem = null;
        Suggestions.IsVisible = false;
        SearchBar.Text = suggestion.Description;

        try
        {
            var location = await _placeSearchProvider.GetLocationAsync(suggestion.PlaceId);
            if (location is not null)
            {
                SetSelectedLocation(location, moveMap: true);
            }
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Recherche", "L’adresse n’a pas pu être récupérée.", "OK");
        }
    }

    private async void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        try
        {
            var address = await _placeSearchProvider.ResolveAddressAsync(e.Location.Latitude, e.Location.Longitude);
            SetSelectedLocation(new LocationResult(
                e.Location.Latitude,
                e.Location.Longitude,
                address ?? string.Empty),
                moveMap: false);
        }
        catch (Exception)
        {
            SetSelectedLocation(new LocationResult(e.Location.Latitude, e.Location.Longitude, string.Empty), moveMap: false);
        }
    }

    private void SetSelectedLocation(LocationResult location, bool moveMap)
    {
        _selectedLocation = location;
        CoordinatesLabel.Text = string.IsNullOrWhiteSpace(location.Address)
            ? $"Latitude : {location.Latitude:F6} · Longitude : {location.Longitude:F6}"
            : $"{location.Address}\nLatitude : {location.Latitude:F6} · Longitude : {location.Longitude:F6}";
        ConfirmButton.IsEnabled = true;

        var mapLocation = new Location(location.Latitude, location.Longitude);
        if (moveMap)
        {
            _locationMap.MoveToRegion(MapSpan.FromCenterAndRadius(mapLocation, Distance.FromKilometers(1)));
        }

        if (_selectedPin is not null)
        {
            _locationMap.Pins.Remove(_selectedPin);
        }

        _selectedPin = new Pin
        {
            Label = "Localisation sélectionnée",
            Address = location.Address,
            Location = mapLocation,
            Type = PinType.Place
        };
        _locationMap.Pins.Add(_selectedPin);
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        if (_selectedLocation is null)
        {
            return;
        }

        var address = string.IsNullOrWhiteSpace(_selectedLocation.Address)
            ? "Adresse non disponible"
            : _selectedLocation.Address;
        var message = $"Adresse : {address}\n" +
                      $"Latitude : {_selectedLocation.Latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}\n" +
                      $"Longitude : {_selectedLocation.Longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}\n\n" +
                      "Appliquer cette localisation au client ?";
        var confirmed = await DisplayAlertAsync("Confirmer la localisation", message, "Appliquer", "Annuler");
        if (!confirmed)
        {
            return;
        }

        _result.TrySetResult(_selectedLocation);
        _cancellationRegistration.Dispose();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_result.Task.IsCompleted)
        {
            _result.TrySetResult(null);
        }

        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _cancellationRegistration.Dispose();
    }

    private void Cancel()
    {
        _result.TrySetCanceled();
        _ = Navigation.PopModalAsync();
    }
}
