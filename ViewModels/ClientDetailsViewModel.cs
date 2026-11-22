using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimateManager.Models;
using UltimateManager.Models.DynamicForms;
using UltimateManager.Services;

namespace UltimateManager.ViewModels;

public partial class ClientDetailsViewModel(
    IClientService clientService,
    IDynamicFormService dynamicFormService,
    ClientDynamicRecordViewModel dynamicRecordViewModel) : ObservableObject
{
    private readonly IClientService _clientService = clientService;
    private readonly IDynamicFormService _dynamicFormService = dynamicFormService;
    private readonly ClientDynamicRecordViewModel _dynamicRecordViewModel = dynamicRecordViewModel;

    public ObservableCollection<DynamicRecordBlockItem> EditableDynamicBlocks => _dynamicRecordViewModel.Blocks;

    public string TemplateName => _dynamicRecordViewModel.TemplateName;

    public Guid TemplateId => _dynamicRecordViewModel.TemplateId;

    [ObservableProperty]
    private Client? currentClient;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private bool isSaving;

    [ObservableProperty]
    private string firstName = string.Empty;

    [ObservableProperty]
    private string lastName = string.Empty;

    [ObservableProperty]
    private string phoneNumber = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string address = string.Empty;

    [ObservableProperty]
    private string postalCode = string.Empty;

    [ObservableProperty]
    private string city = string.Empty;

    [ObservableProperty]
    private string country = string.Empty;

    [ObservableProperty]
    private string comment = string.Empty;

    [ObservableProperty]
    private string latitude = string.Empty;

    [ObservableProperty]
    private string longitude = string.Empty;

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
            LoadEditableClientFields();
            await EnsureDynamicRecordLoadedAsync();
            NotifyComputedProperties();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task BeginEditingAsync()
    {
        if (CurrentClient is null || IsEditing)
        {
            return;
        }

        _dynamicRecordViewModel.BeginEditing();
        IsEditing = true;
    }

    public async Task BeginContentEditingAsync()
    {
        await BeginEditingAsync();
    }

    public async Task<Guid> GetTemplateIdAsync(CancellationToken cancellationToken = default)
    {
        if (TemplateId == Guid.Empty)
        {
            await EnsureDynamicRecordLoadedAsync(cancellationToken);
        }

        return TemplateId;
    }

    [RelayCommand]
    private Task BeginContentEditAsync() => BeginContentEditingAsync();

    public async Task<bool> SaveEditingAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentClient is null || !IsEditing || IsSaving)
        {
            return false;
        }

        try
        {
            IsSaving = true;
            ApplyEditableClientFields(out var clientChanged);
            if (clientChanged)
            {
                await _clientService.UpdateAsync(CurrentClient, cancellationToken);
            }

            if (!await _dynamicRecordViewModel.SaveAsync(cancellationToken))
            {
                return false;
            }

            IsEditing = false;
            NotifyComputedProperties();
            return true;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public async Task CancelEditingAsync()
    {
        if (CurrentClient is null)
        {
            return;
        }

        IsEditing = false;
        await LoadAsync(CurrentClient.Id);
    }

    public Task CallDynamicFieldAsync(DynamicRecordFieldItem field)
    {
        if (!field.IsPhone || string.IsNullOrWhiteSpace(field.PhoneNumber))
        {
            return Task.CompletedTask;
        }

        var number = $"{field.CountryCode}{field.PhoneNumber}".Replace(" ", string.Empty);
        return Launcher.Default.OpenAsync($"tel:{number}");
    }

    public Task PickLocationAsync(DynamicRecordFieldItem field) =>
        _dynamicRecordViewModel.PickLocationAsync(field);

    public Task OpenInMapAsync(DynamicRecordFieldItem field) =>
        _dynamicRecordViewModel.OpenInMapAsync(field);

    private void LoadEditableClientFields()
    {
        FirstName = CurrentClient?.FirstName ?? string.Empty;
        LastName = CurrentClient?.LastName ?? string.Empty;
        PhoneNumber = CurrentClient?.PhoneNumber ?? string.Empty;
        Email = CurrentClient?.Email ?? string.Empty;
        Address = CurrentClient?.Address ?? string.Empty;
        PostalCode = CurrentClient?.PostalCode ?? string.Empty;
        City = CurrentClient?.City ?? string.Empty;
        Country = CurrentClient?.Country ?? string.Empty;
        Comment = CurrentClient?.Comment ?? string.Empty;
        Latitude = CurrentClient?.Latitude?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        Longitude = CurrentClient?.Longitude?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void ApplyEditableClientFields(out bool changed)
    {
        changed = false;
        if (CurrentClient is null)
        {
            return;
        }

        var firstName = FirstName.Trim();
        var lastName = LastName.Trim();
        var phoneNumber = PhoneNumber.Trim();
        var email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        var address = Address.Trim();
        var postalCode = PostalCode.Trim();
        var city = City.Trim();
        var country = Country.Trim();
        var comment = Comment.Trim();
        var latitude = double.TryParse(Latitude.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedLatitude)
            ? (double?)parsedLatitude
            : null;
        var longitudeInput = Longitude.Trim();
        var longitude = double.TryParse(longitudeInput, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedLongitude)
            ? (double?)parsedLongitude
            : null;

        changed = CurrentClient.FirstName != firstName ||
                  CurrentClient.LastName != lastName ||
                  CurrentClient.PhoneNumber != phoneNumber ||
                  CurrentClient.Email != email ||
                  CurrentClient.Address != address ||
                  CurrentClient.PostalCode != postalCode ||
                  CurrentClient.City != city ||
                  CurrentClient.Country != country ||
                  CurrentClient.Comment != comment ||
                  CurrentClient.Latitude != latitude ||
                  CurrentClient.Longitude != longitude;

        if (!changed)
        {
            return;
        }

        CurrentClient.FirstName = firstName;
        CurrentClient.LastName = lastName;
        CurrentClient.PhoneNumber = phoneNumber;
        CurrentClient.Email = email;
        CurrentClient.Address = address;
        CurrentClient.PostalCode = postalCode;
        CurrentClient.City = city;
        CurrentClient.Country = country;
        CurrentClient.Comment = comment;
        CurrentClient.Latitude = latitude;
        CurrentClient.Longitude = longitude;
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

        var latitudeText = latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var longitudeText = longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url = $"https://www.google.com/maps/search/?api=1&query={latitudeText},{longitudeText}";
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

    private async Task EnsureDynamicRecordLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentClient is null)
        {
            return;
        }

        var record = await _dynamicFormService.GetRecordForExternalEntityAsync(CurrentClient.Id, cancellationToken);
        if (record is null)
        {
            var defaultTemplate = await _dynamicFormService.GetDefaultClientTemplateAsync(cancellationToken);
            await _dynamicFormService.EnsureClientTemplateAsync(CurrentClient.Id, defaultTemplate.Template.Id, cancellationToken);
            record = await _dynamicFormService.GetRecordForExternalEntityAsync(CurrentClient.Id, cancellationToken);
        }

        if (record is null)
        {
            return;
        }

        var templateId = record.Record.TemplateId;
        await _dynamicRecordViewModel.LoadAsync(CurrentClient.Id, templateId, cancellationToken);
        OnPropertyChanged(nameof(TemplateName));
        OnPropertyChanged(nameof(TemplateId));
    }
}
