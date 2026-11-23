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
    IDynamicFormService dynamicFormService,
    ClientDynamicRecordViewModel dynamicRecordViewModel) : ObservableValidator
{
    private readonly IClientService _clientService = clientService;
    private readonly IMapProvider _mapProvider = mapProvider;
    private readonly IDynamicFormService _dynamicFormService = dynamicFormService;
    private readonly ClientDynamicRecordViewModel _dynamicRecordViewModel = dynamicRecordViewModel;
    private Guid _clientId;
    private DateTime _createdAt;
    private bool _isExistingClient;
    private Guid _workingTemplateId;
    private bool _templateSelectionLoaded;
    private bool _structureEditing;

    public bool IsEditing => _isExistingClient;

    public bool IsNewClient => !_isExistingClient;

    public bool IsStructureEditing => !_isExistingClient || _structureEditing;

    public bool IsTemplateSelectionLoaded => _templateSelectionLoaded;

    public Guid ClientId => _clientId;

    public ObservableCollection<EntityTemplate> Templates { get; } = [];

    public EntityTemplate EmptyTemplate { get; } = new()
    {
        Id = Guid.Empty,
        Name = "Fiche vide"
    };

    private readonly Dictionary<Guid, string> _templatePreviews = [];

    private EntityTemplate? _selectedTemplate;

    public EntityTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                OnPropertyChanged(nameof(SelectedTemplateId));
                OnPropertyChanged(nameof(SelectedTemplatePreview));
            }
        }
    }

    public Guid SelectedTemplateId => _workingTemplateId != Guid.Empty
        ? _workingTemplateId
        : SelectedTemplate?.Id ?? Guid.Empty;

    public ObservableCollection<DynamicRecordBlockItem> EditableDynamicBlocks => _dynamicRecordViewModel.Blocks;

    public string SelectedTemplatePreview => SelectedTemplate is null
        ? string.Empty
        : _templatePreviews.GetValueOrDefault(SelectedTemplate.Id, "Aucun champ défini");

    public string? FirstNameError => GetErrors(nameof(FirstName)).FirstOrDefault()?.ErrorMessage;

    public string? LastNameError => GetErrors(nameof(LastName)).FirstOrDefault()?.ErrorMessage;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Le prénom est obligatoire.")]
    private string firstName = string.Empty;

    [ObservableProperty]
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

    public void Load(Client? client, bool structureEditing = false)
    {
        _isExistingClient = client is not null;
        _structureEditing = structureEditing;
        _clientId = client?.Id ?? Guid.NewGuid();
        _createdAt = client?.CreatedAt ?? DateTime.UtcNow;
        _workingTemplateId = Guid.Empty;
        _templateSelectionLoaded = false;

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
        OnPropertyChanged(nameof(IsNewClient));
        OnPropertyChanged(nameof(IsStructureEditing));
        OnPropertyChanged(nameof(ClientId));
        OnPropertyChanged(nameof(FirstNameError));
        OnPropertyChanged(nameof(LastNameError));
        OnPropertyChanged(nameof(IsTemplateSelectionLoaded));
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
        Templates.Add(EmptyTemplate);
        _templatePreviews.Clear();
        _templatePreviews[EmptyTemplate.Id] = "Aucun bloc ni champ — vous pourrez les ajouter depuis la fiche client.";
        foreach (var template in templates)
        {
            Templates.Add(template.Template);
            _templatePreviews[template.Template.Id] = string.Join(", ", template.Blocks.Select(block =>
                $"{block.Block.Name} ({block.Fields.Count} champ{(block.Fields.Count > 1 ? "s" : string.Empty)})"));
        }

        EntityTemplate? selected = null;
        EntityRecordModel? existingRecord = null;
        if (_isExistingClient)
        {
            existingRecord = await _dynamicFormService.GetRecordForExternalEntityAsync(clientId, cancellationToken);
            selected = existingRecord is null
                ? null
                : Templates.FirstOrDefault(template => template.Id == existingRecord.Record.TemplateId);
        }

        if (_isExistingClient)
        {
            if (existingRecord is null)
            {
                throw new InvalidOperationException("Le template de la fiche client est introuvable.");
            }

            _workingTemplateId = existingRecord.Record.TemplateId;
            await _dynamicRecordViewModel.LoadAsync(_clientId, _workingTemplateId, cancellationToken);
            SelectedTemplate = selected;
        }
        else
        {
            var defaultTemplate = (await _dynamicFormService.GetDefaultClientTemplateAsync(cancellationToken)).Template;
            SelectedTemplate = Templates.FirstOrDefault(template => template.Id == defaultTemplate.Id) ?? EmptyTemplate;
            await PrepareWorkingTemplateAsync(SelectedTemplate, cancellationToken);
        }
        OnPropertyChanged(nameof(SelectedTemplateId));
        OnPropertyChanged(nameof(SelectedTemplatePreview));
        _templateSelectionLoaded = true;
        OnPropertyChanged(nameof(IsTemplateSelectionLoaded));
    }

    public async Task SelectTemplateAsync(EntityTemplate? template, CancellationToken cancellationToken = default)
    {
        if (!_isExistingClient && template is not null)
        {
            await PrepareWorkingTemplateAsync(template, cancellationToken);
        }
    }

    private async Task PrepareWorkingTemplateAsync(EntityTemplate template, CancellationToken cancellationToken)
    {
        var workingTemplate = template.Id == Guid.Empty
            ? await _dynamicFormService.CreateTemplateAsync($"Fiche de {_clientId:N}", "Client", cancellationToken)
            : await _dynamicFormService.CloneTemplateAsync(template.Id, $"Fiche de {_clientId:N}", cancellationToken);
        _workingTemplateId = workingTemplate.Template.Id;
        await _dynamicRecordViewModel.LoadAsync(_clientId, _workingTemplateId, cancellationToken);
        OnPropertyChanged(nameof(SelectedTemplateId));
    }

    public Task AddBlockAsync(string? name, CancellationToken cancellationToken = default) =>
        _dynamicRecordViewModel.AddBlockAsync(name, cancellationToken);

    public Task AddFieldAsync(DynamicRecordBlockItem block, FieldType fieldType, string? label, CancellationToken cancellationToken = default) =>
        _dynamicRecordViewModel.AddFieldAsync(block, fieldType, label, cancellationToken);

    public Task DeleteBlockAsync(DynamicRecordBlockItem block, CancellationToken cancellationToken = default) =>
        _dynamicRecordViewModel.DeleteBlockAsync(block, cancellationToken);

    public Task DeleteFieldAsync(DynamicRecordBlockItem block, DynamicRecordFieldItem field, CancellationToken cancellationToken = default) =>
        _dynamicRecordViewModel.DeleteFieldAsync(block, field, cancellationToken);

    public Task MoveFieldAsync(DynamicRecordBlockItem block, DynamicRecordFieldItem field, int offset, CancellationToken cancellationToken = default) =>
        _dynamicRecordViewModel.MoveFieldAsync(block, field, offset, cancellationToken);

    public Task MoveBlockAsync(DynamicRecordBlockItem block, int offset, CancellationToken cancellationToken = default) =>
        _dynamicRecordViewModel.MoveBlockAsync(block, offset, cancellationToken);

    public DynamicRecordBlockItem? FindBlock(DynamicRecordFieldItem field) =>
        EditableDynamicBlocks.FirstOrDefault(block => block.Fields.Contains(field));

    public Task PickDynamicLocationAsync(DynamicRecordFieldItem field) =>
        _dynamicRecordViewModel.PickLocationAsync(field);

    public Task OpenDynamicMapAsync(DynamicRecordFieldItem field) =>
        _dynamicRecordViewModel.OpenInMapAsync(field);

    public async Task<bool> SaveDynamicAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamicRecordViewModel.SaveAsync(cancellationToken);
    }

    public Task SaveStructureAsync(CancellationToken cancellationToken = default) =>
        IsStructureEditing ? _dynamicRecordViewModel.SaveTemplateChangesAsync(cancellationToken) : Task.CompletedTask;

    public bool ValidateForSave()
    {
        FirstName = FirstName.Trim();
        LastName = LastName.Trim();
        ValidateAllProperties();
        OnPropertyChanged(nameof(FirstNameError));
        OnPropertyChanged(nameof(LastNameError));
        return !HasErrors &&
               TryParseCoordinate(Latitude, out _) &&
               TryParseCoordinate(Longitude, out _);
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
        if (!ValidateForSave() || IsBusy ||
            !TryParseCoordinate(Latitude, out var latitudeValue) ||
            !TryParseCoordinate(Longitude, out var longitudeValue))
        {
            return false;
        }

        try
        {
            IsBusy = true;
            var client = new Client
            {
                Id = _clientId,
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

            OnPropertyChanged(nameof(ClientId));

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
