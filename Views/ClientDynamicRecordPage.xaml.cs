using Microsoft.Extensions.DependencyInjection;
using UltimateManager.Models.DynamicForms;
using UltimateManager.Services;
using UltimateManager.ViewModels;

namespace UltimateManager.Views;

public partial class ClientDynamicRecordPage : ContentPage, IQueryAttributable
{
    private readonly ClientDynamicRecordViewModel _viewModel;
    private Guid _clientId;
    private Guid? _templateId;

    public ClientDynamicRecordPage(ClientDynamicRecordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("id", out var value) || !Guid.TryParse(value?.ToString(), out _clientId))
        {
            return;
        }

        if (query.TryGetValue("templateId", out var templateValue) && Guid.TryParse(templateValue?.ToString(), out var templateId))
        {
            _templateId = templateId;
        }
        else
        {
            var service = App.Services.GetRequiredService<IDynamicFormService>();
            var record = await service.GetRecordForExternalEntityAsync(_clientId);
            _templateId = record?.Record.TemplateId ?? (await service.GetDefaultClientTemplateAsync()).Template.Id;
        }

        await _viewModel.LoadAsync(_clientId, _templateId.Value);
    }

    private async void OnPickLocationClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: DynamicRecordFieldItem field })
        {
            await _viewModel.PickLocationAsync(field);
        }
    }

    private async void OnPickDocumentClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: DynamicRecordFieldItem field })
        {
            return;
        }

        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Sélectionner un document"
        });
        if (file is null)
        {
            return;
        }

        var documentsDirectory = Path.Combine(FileSystem.AppDataDirectory, "documents");
        Directory.CreateDirectory(documentsDirectory);
        var destination = Path.Combine(documentsDirectory, $"{Guid.NewGuid()}_{file.FileName}");
        await using var source = await file.OpenReadAsync();
        await using var target = File.Create(destination);
        await source.CopyToAsync(target);
        field.DocumentPath = destination;
    }

    private async void OnAddFieldClicked(object? sender, EventArgs e)
    {
        var block = await SelectBlockAsync();
        if (block is null)
        {
            return;
        }

        var fieldTypes = new Dictionary<string, FieldType>
        {
            ["Texte court"] = FieldType.ShortText,
            ["Texte long"] = FieldType.LongText,
            ["Nombre"] = FieldType.Number,
            ["Date"] = FieldType.Date,
            ["Téléphone"] = FieldType.Phone,
            ["Adresse"] = FieldType.Address,
            ["Coordonnées GPS"] = FieldType.GeoPoint,
            ["Email"] = FieldType.Email,
            ["URL"] = FieldType.Url,
            ["Case à cocher"] = FieldType.Checkbox,
            ["Liste déroulante"] = FieldType.Dropdown,
            ["Commentaire"] = FieldType.Comment,
            ["Document"] = FieldType.Document
        };
        var selectedType = await DisplayActionSheet(
            "Type du champ",
            "Annuler",
            null,
            fieldTypes.Keys.ToArray());
        if (selectedType is null || !fieldTypes.TryGetValue(selectedType, out var fieldType))
        {
            return;
        }

        var label = await DisplayPromptAsync(
            "Libellé du champ",
            "Libellé facultatif",
            "Ajouter",
            "Annuler");
        if (label is null)
        {
            return;
        }

        await _viewModel.AddFieldAsync(block, fieldType, label);
    }

    private async Task<DynamicRecordBlockItem?> SelectBlockAsync()
    {
        if (_viewModel.Blocks.Count == 1)
        {
            return _viewModel.Blocks[0];
        }

        var names = _viewModel.Blocks.Select(block => block.Name).ToArray();
        var selected = await DisplayActionSheet("Bloc cible", "Annuler", null, names);
        return selected is null || selected == "Annuler"
            ? null
            : _viewModel.Blocks.FirstOrDefault(block => block.Name == selected);
    }

    private async void OnEditTemplateClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"TemplateEditorPage?id={_templateId}");
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (await _viewModel.SaveAsync())
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
