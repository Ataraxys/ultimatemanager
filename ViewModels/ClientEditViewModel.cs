using System.ComponentModel.DataAnnotations;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimateManager.Models;
using UltimateManager.Models.DynamicForms;
using UltimateManager.Services;

namespace UltimateManager.ViewModels;

public partial class ClientEditViewModel(
    IClientService clientService,
    IMapProvider mapProvider,
    IDynamicFormService dynamicFormService) : ObservableValidator
{
    private readonly IClientService _clientService = clientService;
    private readonly IMapProvider _mapProvider = mapProvider;
    private readonly IDynamicFormService _dynamicFormService = dynamicFormService;
    private Guid _clientId;
    private DateTime _createdAt;

    public bool IsEditing => _clientId != Guid.Empty;

    public Guid ClientId => _clientId;

    public ObservableCollection<EntityTemplate> Templates { get; } = [];

    private EntityTemplate? _selectedTemplate;

    public EntityTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                OnPropertyChanged(nameof(SelectedTemplateId));
            }
        }
    }

    public Guid SelectedTemplateId => SelectedTemplate?.Id ?? Guid.Empty;

    public string? FirstNameError => GetErrors(nameof(FirstName)).FirstOrDefault()?.ErrorMessage;

    public string? LastNameError => GetErrors(nameof(LastName)).FirstOrDefault()?.ErrorMessage;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Le prénom est obligatoire.")]
    private string firstName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Le nom est obligatoire.")]
    private string lastName = string.Empty;

    [ObservableProperty]
    private string phoneNumber = string.Empty;

    [ObservableProperty]
    private string? email;

    [ObservableProperty]
    private string address = string.Empty;

    [ObservableProperty]
    private string postalCode = string.Empty;

    [ObservableProperty]
    private string city = string.Empty;

    [ObservableProperty]
    private string country = string.Empty;

    [ObservableProperty]
    private string? latitude;

    [ObservableProperty]
    private string? longitude;

    [ObservableProperty]
    private string comment = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public void Load(Client? client)
    {
        _clientId = client?.Id ?? Guid.Empty;
        _createdAt = client?.CreatedAt ?? DateTime.UtcNow;

        FirstName = client?.FirstName ?? string.Empty;
        LastName = client?.LastName ?? string.Empty;
        PhoneNumber = client?.PhoneNumber ?? string.Empty;
        Email = client?.Email;
        Address = client?.Address ?? string.Empty;
        PostalCode = client?.PostalCode ?? string.Empty;
        City = client?.City ?? string.Empty;
        Country = client?.Country ?? string.Empty;
        Latitude = client?.Latitude?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Longitude = client?.Longitude?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Comment = client?.Comment ?? string.Empty;
        ClearErrors();
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(ClientId));
        OnPropertyChanged(nameof(FirstNameError));
        OnPropertyChanged(nameof(LastNameError));
    }

    public async Task LoadTemplateSelectionAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var templates = await _dynamicFormService.GetTemplatesAsync(cancellationToken);
        if (templates.Count == 0)
        {
            await _dynamicFormService.EnsureDefaultClientTemplateAsync(cancellationToken);
            templates = await _dynamicFormService.GetTemplatesAsync(cancellationToken);
        }

        Templates.Clear();
        foreach (var template in templates)
        {
            Templates.Add(template.Template);
        }

        EntityTemplate? selected = null;
        if (clientId != Guid.Empty)
        {
            var record = await _dynamicFormService.GetRecordForExternalEntityAsync(clientId, cancellationToken);
            selected = record is null
                ? null
                : Templates.FirstOrDefault(template => template.Id == record.Record.TemplateId);
        }

        var defaultTemplate = (await _dynamicFormService.GetDefaultClientTemplateAsync(cancellationToken)).Template;
        SelectedTemplate = selected ?? Templates.FirstOrDefault(template => template.Id == defaultTemplate.Id) ?? defaultTemplate;
        OnPropertyChanged(nameof(SelectedTemplateId));
    }

    [RelayCommand]
    private async Task PickLocationAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var latitude = TryParseCoordinate(Latitude, out var currentLatitude) ? currentLatitude : null;
        var longitude = TryParseCoordinate(Longitude, out var currentLongitude) ? currentLongitude : null;
        var initialLocation = latitude.HasValue && longitude.HasValue
            ? new LocationResult(latitude.Value, longitude.Value, Address)
            : null;
        var location = await _mapProvider.PickAsync(initialLocation);
        if (location is null)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() => ApplyLocation(location));
    }

    public void ApplyLocation(LocationResult location)
    {
        Latitude = location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Longitude = location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Address = NormalizeAddress(location.Address);
    }

    private static string NormalizeAddress(string? address)
    {
        return string.IsNullOrWhiteSpace(address)
            ? string.Empty
            : string.Join(", ", address.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    [RelayCommand]
    public async Task<bool> SaveAsync()
    {
        FirstName = FirstName.Trim();
        LastName = LastName.Trim();
        ValidateAllProperties();
        OnPropertyChanged(nameof(FirstNameError));
        OnPropertyChanged(nameof(LastNameError));
        if (HasErrors || IsBusy)
        {
            return false;
        }

        if (!TryParseCoordinate(Latitude, out var latitudeValue) ||
            !TryParseCoordinate(Longitude, out var longitudeValue))
        {
            return false;
        }

        try
        {
            IsBusy = true;
            var client = new Client
            {
                Id = _clientId == Guid.Empty ? Guid.NewGuid() : _clientId,
                FirstName = FirstName.Trim(),
                LastName = LastName.Trim(),
                PhoneNumber = PhoneNumber.Trim(),
                Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                Address = Address.Trim(),
                PostalCode = PostalCode.Trim(),
                City = City.Trim(),
                Country = Country.Trim(),
                Latitude = latitudeValue,
                Longitude = longitudeValue,
                Comment = Comment.Trim(),
                CreatedAt = _createdAt,
                UpdatedAt = DateTime.UtcNow
            };

            if (IsEditing)
            {
                await _clientService.UpdateAsync(client);
            }
            else
            {
                await _clientService.CreateAsync(client);
            }

            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool TryParseCoordinate(string? value, out double? coordinate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            coordinate = null;
            return true;
        }

        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            coordinate = parsed;
            return true;
        }

        coordinate = null;
        return false;
    }
}
