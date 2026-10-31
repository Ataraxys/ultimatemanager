using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimateManager.Models;
using UltimateManager.Services;

namespace UltimateManager.ViewModels;

public partial class ClientDetailsViewModel(IClientService clientService) : ObservableObject
{
    private readonly IClientService _clientService = clientService;

    [ObservableProperty]
    private Client? currentClient;

    [ObservableProperty]
    private bool isLoading;

    public bool HasLocation => CurrentClient?.Latitude is not null && CurrentClient.Longitude is not null;

    public bool HasPhone => !string.IsNullOrWhiteSpace(CurrentClient?.PhoneNumber);

    public bool HasAddress => !string.IsNullOrWhiteSpace(CurrentClient?.Address) ||
                              !string.IsNullOrWhiteSpace(CurrentClient?.PostalCode) ||
                              !string.IsNullOrWhiteSpace(CurrentClient?.City) ||
                              !string.IsNullOrWhiteSpace(CurrentClient?.Country);

    public bool HasComment => !string.IsNullOrWhiteSpace(CurrentClient?.Comment);

    public string FullAddress => string.Join(", ", new[]
    {
        CurrentClient?.Address,
        CurrentClient?.PostalCode,
        CurrentClient?.City,
        CurrentClient?.Country
    }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public async Task LoadAsync(Guid id)
    {
        try
        {
            IsLoading = true;
            CurrentClient = await _clientService.GetByIdAsync(id);
            NotifyComputedProperties();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CallAsync()
    {
        if (!HasPhone || CurrentClient is null)
        {
            return;
        }

        await Launcher.Default.OpenAsync($"tel:{CurrentClient.PhoneNumber}");
    }

    [RelayCommand]
    private async Task OpenMapAsync()
    {
        if (!HasLocation || CurrentClient is null)
        {
            return;
        }

        var latitude = CurrentClient.Latitude;
        var longitude = CurrentClient.Longitude;
        if (latitude is null || longitude is null)
        {
            return;
        }

        var url = $"https://www.google.com/maps/dir/?api=1&destination={latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        await Launcher.Default.OpenAsync(url);
    }

    [RelayCommand]
    private async Task CopyAddressAsync()
    {
        if (HasAddress)
        {
            await Clipboard.Default.SetTextAsync(FullAddress);
        }
    }

    private void NotifyComputedProperties()
    {
        OnPropertyChanged(nameof(HasLocation));
        OnPropertyChanged(nameof(HasPhone));
        OnPropertyChanged(nameof(HasAddress));
        OnPropertyChanged(nameof(HasComment));
        OnPropertyChanged(nameof(FullAddress));
    }
}
