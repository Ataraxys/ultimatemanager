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
    DynamicFieldValueSerializer serializer) : ObservableObject
{
    private readonly IClientService _clientService = clientService;
    private readonly IDynamicFormService _dynamicFormService = dynamicFormService;
    private readonly DynamicFieldValueSerializer _serializer = serializer;

    public ObservableCollection<DynamicBlockDisplay> DynamicBlocks { get; } = [];

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
            await LoadDynamicBlocksAsync();
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

    private async Task LoadDynamicBlocksAsync()
    {
        DynamicBlocks.Clear();
        if (CurrentClient is null)
        {
            return;
        }

        var record = await _dynamicFormService.GetRecordForExternalEntityAsync(CurrentClient.Id);
        if (record is null)
        {
            var defaultTemplate = await _dynamicFormService.GetDefaultClientTemplateAsync();
            record = await _dynamicFormService.GetOrCreateRecordAsync(CurrentClient.Id, defaultTemplate.Template.Id);
        }

        var template = await _dynamicFormService.GetTemplateAsync(record.Record.TemplateId)
            ?? await _dynamicFormService.GetDefaultClientTemplateAsync();
        foreach (var block in template.Blocks)
        {
            var instance = record.Blocks.FirstOrDefault(item => item.Block.BlockDefinitionId == block.Block.Id);
            var values = instance?.Values.ToDictionary(value => value.FieldDefinitionId) ?? [];
            DynamicBlocks.Add(new DynamicBlockDisplay
            {
                Name = block.Block.Name,
                Fields = block.Fields.Select(field => new DynamicFieldDisplay
                {
                    Label = field.Label,
                    FieldType = field.FieldType,
                    Value = values.TryGetValue(field.Id, out var savedValue)
                        ? GetStoredValue(field.FieldType, savedValue.ValueJson)
                        : GetLegacyValue(field.Label)
                }).ToList()
            });
        }
    }

    private string GetStoredValue(FieldType fieldType, string json)
    {
        try
        {
            return fieldType switch
            {
                FieldType.Phone => _serializer.Deserialize<PhoneNumberValue>(json) is { } phone
                    ? $"{phone.CountryCode} {phone.Number}".Trim()
                    : string.Empty,
                FieldType.Address => _serializer.Deserialize<AddressValue>(json)?.ToDisplayString() ?? string.Empty,
                FieldType.GeoPoint => _serializer.Deserialize<GeoPointValue>(json) is { } point
                    ? $"{point.Latitude}, {point.Longitude}"
                    : string.Empty,
                FieldType.Comment => _serializer.Deserialize<CommentValue>(json)?.Text ?? string.Empty,
                FieldType.Document => _serializer.Deserialize<DocumentValue>(json)?.FileName ?? string.Empty,
                FieldType.Checkbox => System.Text.Json.JsonSerializer.Deserialize<bool>(json) ? "Oui" : "Non",
                FieldType.Date => System.Text.Json.JsonSerializer.Deserialize<DateTime>(json).ToString("d"),
                _ => System.Text.Json.JsonSerializer.Deserialize<string>(json) ?? string.Empty
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }

    private string GetLegacyValue(string label)
    {
        if (CurrentClient is null)
        {
            return string.Empty;
        }

        return label switch
        {
            "Prénom" => CurrentClient.FirstName,
            "Nom" => CurrentClient.LastName,
            "Téléphone" => CurrentClient.PhoneNumber,
            "Email" => CurrentClient.Email ?? string.Empty,
            "Adresse" => FullAddress,
            "Localisation GPS" when HasLocation => $"{CurrentClient.Latitude}, {CurrentClient.Longitude}",
            "Commentaire" => CurrentClient.Comment,
            _ => string.Empty
        };
    }
}
