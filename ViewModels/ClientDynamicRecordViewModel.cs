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

    public ObservableCollection<DynamicRecordBlockItem> Blocks { get; } = [];

    public string TemplateName { get; private set; } = string.Empty;

    public bool IsBusy { get; private set; }

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
            TemplateName = template.Template.Name;
            OnPropertyChanged(nameof(TemplateName));
            Blocks.Clear();
            foreach (var block in template.Blocks)
            {
                var instance = record.Blocks.FirstOrDefault(item => item.Block.BlockDefinitionId == block.Block.Id);
                var values = instance?.Values.ToDictionary(value => value.FieldDefinitionId) ?? [];
                Blocks.Add(new DynamicRecordBlockItem(
                    block.Block.Id,
                    block.Block.Name,
                    block.Fields.Select(field => new DynamicRecordFieldItem(field, values.GetValueOrDefault(field.Id), _serializer)).ToList()));
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
            foreach (var field in Blocks.SelectMany(block => block.Fields))
            {
                await _formService.SaveFieldValueAsync(
                    _recordId,
                    field.Id,
                    field.SerializeValue(),
                    cancellationToken);
            }
            await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
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

public sealed class DynamicRecordBlockItem(Guid blockDefinitionId, string name, IEnumerable<DynamicRecordFieldItem> fields)
{
    public Guid BlockDefinitionId { get; } = blockDefinitionId;
    public string Name { get; } = name;
    public IList<DynamicRecordFieldItem> Fields { get; } = fields.ToList();
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

    public DynamicRecordFieldItem(FieldDefinition definition, FieldValue? savedValue, DynamicFieldValueSerializer serializer)
    {
        Definition = definition;
        _serializer = serializer;
        Options = DeserializeOptions(definition.OptionsJson);
        LoadValue(savedValue?.ValueJson);
    }

    public FieldDefinition Definition { get; }
    public Guid Id => Definition.Id;
    public string Label => Definition.Label;
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

    public string Value { get => value; set => SetProperty(ref this.value, value); }
    public bool BooleanValue { get => booleanValue; set => SetProperty(ref booleanValue, value); }
    public DateTime DateValue { get => dateValue; set => SetProperty(ref dateValue, value); }
    public string PhoneNumber { get => phoneNumber; set => SetProperty(ref phoneNumber, value); }
    public string CountryCode { get => countryCode; set => SetProperty(ref countryCode, value); }
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
            Country = Country
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
                    Address = addressValue?.Street ?? string.Empty;
                    AddressNumber = addressValue?.Number ?? string.Empty;
                    PostalCode = addressValue?.PostalCode ?? string.Empty;
                    City = addressValue?.City ?? string.Empty;
                    Country = addressValue?.Country ?? string.Empty;
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
