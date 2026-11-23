using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using UltimateManager.Models.DynamicForms;
using UltimateManager.Services;

namespace UltimateManager.ViewModels;

public sealed class ClientDynamicRecordViewModel(
    IDynamicFormService formService,
    DynamicFieldValueSerializer serializer,
    IMapProvider mapProvider,
    IAutomaticBackupService automaticBackupService) : ObservableObject
{
    private readonly IDynamicFormService _formService = formService;
    private readonly DynamicFieldValueSerializer _serializer = serializer;
    private readonly IMapProvider _mapProvider = mapProvider;
    private readonly IAutomaticBackupService _automaticBackupService = automaticBackupService;
    private Guid _recordId;
    private Guid _templateId;
    private readonly Dictionary<Guid, string> _initialFieldValues = [];

    public ObservableCollection<DynamicRecordBlockItem> Blocks { get; } = [];

    public string TemplateName { get; private set; } = string.Empty;

    public Guid TemplateId => _templateId;

    public bool IsBusy { get; private set; }

    public void BeginEditing()
    {
        _initialFieldValues.Clear();
        foreach (var field in Blocks.SelectMany(block => block.Fields))
        {
            _initialFieldValues[field.Id] = field.SerializeValue();
        }
    }

    public async Task LoadAsync(Guid clientId, Guid templateId, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        OnPropertyChanged(nameof(IsBusy));
        try
        {
            var record = await _formService.GetOrCreateRecordAsync(clientId, templateId, cancellationToken);
            var template = await _formService.GetTemplateAsync(templateId, cancellationToken)
                ?? throw new InvalidOperationException("Le modèle demandé n’existe pas.");
            _recordId = record.Record.Id;
            _templateId = template.Template.Id;
            TemplateName = template.Template.Name;
            OnPropertyChanged(nameof(TemplateName));
            Blocks.Clear();
            _initialFieldValues.Clear();
            foreach (var block in template.Blocks)
            {
                var instance = record.Blocks.FirstOrDefault(item => item.Block.BlockDefinitionId == block.Block.Id);
                var values = instance?.Values.ToDictionary(value => value.FieldDefinitionId) ?? [];
                var hasAddress = block.Fields.Any(field => field.FieldType == FieldType.Address);
                var fields = block.Fields
                    .Where(field => !hasAddress || field.FieldType != FieldType.GeoPoint)
                    .Select(field => new DynamicRecordFieldItem(field, values.GetValueOrDefault(field.Id), _serializer))
                    .ToList();
                var addressField = fields.FirstOrDefault(field => field.IsAddress);
                var legacyGeoPoint = block.Fields.FirstOrDefault(field => field.FieldType == FieldType.GeoPoint);
                if (addressField is not null && legacyGeoPoint is not null && values.TryGetValue(legacyGeoPoint.Id, out var geoValue) &&
                    !addressField.TryGetCoordinates(out _, out _))
                {
                    var point = _serializer.Deserialize<GeoPointValue>(geoValue.ValueJson);
                    if (point is not null)
                    {
                        addressField.Latitude = point.Latitude.ToString(CultureInfo.InvariantCulture);
                        addressField.Longitude = point.Longitude.ToString(CultureInfo.InvariantCulture);
                    }
                }
                var blockItem = new DynamicRecordBlockItem(
                    block.Block.Id,
                    block.Block.Name,
                    fields);
                Blocks.Add(blockItem);
                foreach (var field in blockItem.Fields)
                {
                    _initialFieldValues[field.Id] = field.SerializeValue();
                }
            }
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_recordId == Guid.Empty || IsBusy)
        {
            return false;
        }

        IsBusy = true;
        OnPropertyChanged(nameof(IsBusy));
        try
        {
            var pendingValues = new Dictionary<Guid, string>();
            foreach (var field in Blocks.SelectMany(block => block.Fields))
            {
                var serializedValue = field.SerializeValue();
                if (_initialFieldValues.TryGetValue(field.Id, out var initialValue) &&
                    string.Equals(initialValue, serializedValue, StringComparison.Ordinal))
                {
                    continue;
                }

                pendingValues[field.Id] = serializedValue;
            }

            if (pendingValues.Count > 0)
            {
                await _formService.SaveFieldValuesAsync(_recordId, pendingValues, cancellationToken);
                foreach (var (fieldId, value) in pendingValues)
                {
                    _initialFieldValues[fieldId] = value;
                }

                await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
            }

            return true;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public async Task PickLocationAsync(DynamicRecordFieldItem field)
    {
        var latitude = double.TryParse(field.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ? lat : (double?)null;
        var longitude = double.TryParse(field.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ? lon : (double?)null;
        var initial = latitude.HasValue && longitude.HasValue
            ? new LocationResult(latitude.Value, longitude.Value, field.Address)
            : null;
        var result = await _mapProvider.PickAsync(initial);
        if (result is null)
        {
            return;
        }

        field.Latitude = result.Latitude.ToString(CultureInfo.InvariantCulture);
        field.Longitude = result.Longitude.ToString(CultureInfo.InvariantCulture);
        field.Address = result.Address ?? field.Address;
    }

    public async Task OpenInMapAsync(DynamicRecordFieldItem field)
    {
        var query = field.TryGetCoordinates(out var latitude, out var longitude)
            ? $"{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}"
            : field.Address;
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var url = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(query)}";
        await Launcher.Default.OpenAsync(url);
    }

    public async Task AddFieldAsync(
        DynamicRecordBlockItem block,
        FieldType fieldType,
        string? label,
        CancellationToken cancellationToken = default)
    {
        var field = await _formService.AddFieldAsync(
            block.BlockDefinitionId,
            string.IsNullOrWhiteSpace(label) ? GetDefaultLabel(fieldType) : label.Trim(),
            fieldType,
            cancellationToken);
        block.Fields.Add(new DynamicRecordFieldItem(field, null, _serializer));
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    public async Task<DynamicRecordBlockItem> AddBlockAsync(
        string? name,
        CancellationToken cancellationToken = default)
    {
        var block = await _formService.AddBlockAsync(
            _templateId,
            string.IsNullOrWhiteSpace(name) ? "Nouveau bloc" : name.Trim(),
            cancellationToken);
        var item = new DynamicRecordBlockItem(block.Id, block.Name, []);
        Blocks.Add(item);
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
        return item;
    }

    public async Task MoveBlockAsync(
        DynamicRecordBlockItem block,
        int offset,
        CancellationToken cancellationToken = default)
    {
        await _formService.MoveBlockAsync(_templateId, block.BlockDefinitionId, offset, cancellationToken);
        var index = Blocks.IndexOf(block);
        var target = Math.Clamp(index + offset, 0, Blocks.Count - 1);
        if (index >= 0 && index != target)
        {
            Blocks.Move(index, target);
        }

        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    public async Task DeleteBlockAsync(
        DynamicRecordBlockItem block,
        CancellationToken cancellationToken = default)
    {
        await _formService.DeleteBlockAsync(block.BlockDefinitionId, cancellationToken);
        Blocks.Remove(block);
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    public async Task DeleteFieldAsync(
        DynamicRecordBlockItem block,
        DynamicRecordFieldItem field,
        CancellationToken cancellationToken = default)
    {
        await _formService.DeleteFieldAsync(field.Id, cancellationToken);
        block.Fields.Remove(field);
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    public async Task MoveFieldAsync(
        DynamicRecordBlockItem block,
        DynamicRecordFieldItem field,
        int offset,
        CancellationToken cancellationToken = default)
    {
        await _formService.MoveFieldAsync(block.BlockDefinitionId, field.Id, offset, cancellationToken);
        var index = block.Fields.IndexOf(field);
        var target = Math.Clamp(index + offset, 0, block.Fields.Count - 1);
        if (index != target)
        {
            block.Fields.RemoveAt(index);
            block.Fields.Insert(target, field);
        }

        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    public async Task SaveTemplateChangesAsync(CancellationToken cancellationToken = default)
    {
        var template = await _formService.GetTemplateAsync(_templateId, cancellationToken)
            ?? throw new InvalidOperationException("Le modèle demandé n’existe pas.");

        foreach (var blockItem in Blocks)
        {
            var block = template.Blocks.FirstOrDefault(item => item.Block.Id == blockItem.BlockDefinitionId)?.Block;
            if (block is null)
            {
                continue;
            }

            block.Name = blockItem.Name.Trim();
            await _formService.UpdateBlockAsync(block, cancellationToken);
            foreach (var fieldItem in blockItem.Fields)
            {
                fieldItem.Definition.Label = fieldItem.Label.Trim();
                await _formService.UpdateFieldAsync(fieldItem.Definition, cancellationToken);
            }
        }
    }

    private static string GetDefaultLabel(FieldType fieldType) => fieldType switch
    {
        FieldType.ShortText => "Texte",
        FieldType.LongText => "Texte long",
        FieldType.Number => "Nombre",
        FieldType.Date => "Date",
        FieldType.Phone => "Téléphone",
        FieldType.Address => "Adresse",
        FieldType.GeoPoint => "Coordonnées GPS",
        FieldType.Email => "Email",
        FieldType.Url => "URL",
        FieldType.Checkbox => "Case à cocher",
        FieldType.Dropdown => "Liste déroulante",
        FieldType.Comment => "Commentaire",
        FieldType.Document => "Document",
        _ => "Nouveau champ"
    };
}

public sealed class DynamicRecordBlockItem(Guid blockDefinitionId, string name, IEnumerable<DynamicRecordFieldItem> fields) : ObservableObject
{
    public Guid BlockDefinitionId { get; } = blockDefinitionId;
    private string name = name;

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public ObservableCollection<DynamicRecordFieldItem> Fields { get; } = new(fields);
}

public sealed class DynamicRecordFieldItem : ObservableObject
{
    private readonly DynamicFieldValueSerializer _serializer;
    private string value = string.Empty;
    private bool booleanValue;
    private DateTime dateValue = DateTime.Today;
    private string phoneNumber = string.Empty;
    private string countryCode = string.Empty;
    private bool isPrimaryPhone;
    private string address = string.Empty;
    private string addressNumber = string.Empty;
    private string postalCode = string.Empty;
    private string city = string.Empty;
    private string country = string.Empty;
    private string latitude = string.Empty;
    private string longitude = string.Empty;
    private string documentPath = string.Empty;
    private string label;

    public DynamicRecordFieldItem(FieldDefinition definition, FieldValue? savedValue, DynamicFieldValueSerializer serializer)
    {
        Definition = definition;
        _serializer = serializer;
        label = definition.Label;
        Options = DeserializeOptions(definition.OptionsJson);
        LoadValue(savedValue?.ValueJson);
    }

    public FieldDefinition Definition { get; }
    public Guid Id => Definition.Id;
    public string Label
    {
        get => label;
        set => SetProperty(ref label, value);
    }
    public FieldType FieldType => Definition.FieldType;
    public IReadOnlyList<string> Options { get; }
    public bool IsLongText => FieldType is FieldType.LongText or FieldType.Comment;
    public bool IsSimple => !IsLongText && !IsCheckbox && !IsDate && !IsDropdown && !IsPhone && !IsAddress && !IsGeoPoint && !IsDocument;
    public bool IsCheckbox => FieldType == FieldType.Checkbox;
    public bool IsDate => FieldType == FieldType.Date;
    public bool IsDropdown => FieldType == FieldType.Dropdown;
    public bool IsPhone => FieldType == FieldType.Phone;
    public bool IsAddress => FieldType == FieldType.Address;
    public bool IsGeoPoint => FieldType == FieldType.GeoPoint;
    public bool IsDocument => FieldType == FieldType.Document;
    public bool HasMapAction => IsAddress || IsGeoPoint;
    public bool HasPhoneValue => IsPhone && !string.IsNullOrWhiteSpace(PhoneNumber);

    public string Value { get => value; set => SetProperty(ref this.value, value); }
    public bool BooleanValue { get => booleanValue; set => SetProperty(ref booleanValue, value); }
    public DateTime DateValue { get => dateValue; set => SetProperty(ref dateValue, value); }
    public string PhoneNumber
    {
        get => phoneNumber;
        set
        {
            if (SetProperty(ref phoneNumber, value))
            {
                OnPropertyChanged(nameof(HasPhoneValue));
            }
        }
    }
    public string CountryCode
    {
        get => countryCode;
        set
        {
            if (SetProperty(ref countryCode, value))
            {
                OnPropertyChanged(nameof(HasPhoneValue));
            }
        }
    }
    public bool IsPrimaryPhone { get => isPrimaryPhone; set => SetProperty(ref isPrimaryPhone, value); }
    public string Address { get => address; set => SetProperty(ref address, value); }
    public string AddressNumber { get => addressNumber; set => SetProperty(ref addressNumber, value); }
    public string PostalCode { get => postalCode; set => SetProperty(ref postalCode, value); }
    public string City { get => city; set => SetProperty(ref city, value); }
    public string Country { get => country; set => SetProperty(ref country, value); }
    public string Latitude { get => latitude; set => SetProperty(ref latitude, value); }
    public string Longitude { get => longitude; set => SetProperty(ref longitude, value); }
    public string DocumentPath { get => documentPath; set => SetProperty(ref documentPath, value); }

    public string SerializeValue() => FieldType switch
    {
        FieldType.Phone => _serializer.Serialize(new PhoneNumberValue { CountryCode = CountryCode, Number = PhoneNumber, IsPrimary = IsPrimaryPhone }),
        FieldType.Address => _serializer.Serialize(new AddressValue
        {
            Street = Address,
            Number = AddressNumber,
            PostalCode = PostalCode,
            City = City,
            Country = Country,
            Location = TryGetCoordinates(out var latitude, out var longitude)
                ? new GeoPointValue { Latitude = latitude, Longitude = longitude }
                : null
        }),
        FieldType.GeoPoint => _serializer.Serialize(new GeoPointValue
        {
            Latitude = ParseCoordinate(Latitude),
            Longitude = ParseCoordinate(Longitude)
        }),
        FieldType.Comment => _serializer.Serialize(new CommentValue { Text = Value }),
        FieldType.Document => _serializer.Serialize(new DocumentValue { FilePath = DocumentPath, FileName = Path.GetFileName(DocumentPath) }),
        FieldType.Checkbox => JsonSerializer.Serialize(BooleanValue),
        FieldType.Date => JsonSerializer.Serialize(DateValue),
        _ => JsonSerializer.Serialize(Value)
    };

    private void LoadValue(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            switch (FieldType)
            {
                case FieldType.Phone:
                    var phone = _serializer.Deserialize<PhoneNumberValue>(json);
                    CountryCode = phone?.CountryCode ?? string.Empty;
                    PhoneNumber = phone?.Number ?? string.Empty;
                    IsPrimaryPhone = phone?.IsPrimary ?? false;
                    break;
                case FieldType.Address:
                    var addressValue = _serializer.Deserialize<AddressValue>(json);
                    Address = addressValue?.ToDisplayString() ?? string.Empty;
                    AddressNumber = addressValue?.Number ?? string.Empty;
                    PostalCode = addressValue?.PostalCode ?? string.Empty;
                    City = addressValue?.City ?? string.Empty;
                    Country = addressValue?.Country ?? string.Empty;
                    Latitude = addressValue?.Location?.Latitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                    Longitude = addressValue?.Location?.Longitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                    break;
                case FieldType.GeoPoint:
                    var point = _serializer.Deserialize<GeoPointValue>(json);
                    Latitude = point?.Latitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                    Longitude = point?.Longitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                    break;
                case FieldType.Comment:
                    Value = _serializer.Deserialize<CommentValue>(json)?.Text ?? string.Empty;
                    break;
                case FieldType.Document:
                    DocumentPath = _serializer.Deserialize<DocumentValue>(json)?.FilePath ?? string.Empty;
                    break;
                case FieldType.Checkbox:
                    BooleanValue = JsonSerializer.Deserialize<bool>(json);
                    break;
                case FieldType.Date:
                    DateValue = JsonSerializer.Deserialize<DateTime>(json);
                    break;
                default:
                    Value = JsonSerializer.Deserialize<string>(json) ?? string.Empty;
                    break;
            }

        }
        catch (JsonException)
        {
            Value = json;
        }
    }

    public bool TryGetCoordinates(out double latitude, out double longitude)
    {
        var latitudeValid = double.TryParse(Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude);
        var longitudeValid = double.TryParse(Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude);
        return latitudeValid && longitudeValid;
    }

    private static IReadOnlyList<string> DeserializeOptions(string optionsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(optionsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static double ParseCoordinate(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
