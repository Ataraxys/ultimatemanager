using Microsoft.Extensions.DependencyInjection;
using UltimateManager.Models;
using UltimateManager.Models.DynamicForms;
using UltimateManager.ViewModels;

namespace UltimateManager.Views;

public partial class ClientEditPage : ContentPage, IQueryAttributable
{
    private readonly ClientEditViewModel _viewModel;

    public ClientEditPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<ClientEditViewModel>();
        BindingContext = _viewModel;
        _viewModel.Load(null);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_viewModel.IsTemplateSelectionLoaded)
        {
            await _viewModel.LoadTemplateSelectionAsync(Guid.Empty);
        }
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("id", out var value) || !Guid.TryParse(value?.ToString(), out var id))
        {
            _viewModel.Load(null);
            return;
        }

        var modeValue = query.TryGetValue("mode", out var mode) ? mode?.ToString() : "content";
        var structureEditing = string.Equals(modeValue, "structure", StringComparison.OrdinalIgnoreCase);
        if (!structureEditing && !string.Equals(modeValue, "content", StringComparison.OrdinalIgnoreCase))
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        var clientService = App.Services.GetRequiredService<Services.IClientService>();
        var client = await clientService.GetByIdAsync(id);
        _viewModel.Load(client, structureEditing);
        await _viewModel.LoadTemplateSelectionAsync(id);
    }

    private async void OnTemplateChanged(object? sender, EventArgs e)
    {
        if (sender is Picker { SelectedItem: EntityTemplate template })
        {
            await _viewModel.SelectTemplateAsync(template);
        }
    }

    private async void OnAddBlockClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Nouveau bloc", "Nom du bloc", "Ajouter", "Annuler");
        if (name is not null)
        {
            await _viewModel.AddBlockAsync(name);
        }
    }

    private async void OnDeleteBlockClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: DynamicRecordBlockItem block } ||
            !await DisplayAlertAsync("Supprimer le bloc", $"Supprimer « {block.Name} » ?", "Supprimer", "Annuler"))
        {
            return;
        }

        await _viewModel.DeleteBlockAsync(block);
    }

    private async void OnMoveBlockUpClicked(object? sender, EventArgs e) => await MoveBlockAsync(sender, -1);

    private async void OnMoveBlockDownClicked(object? sender, EventArgs e) => await MoveBlockAsync(sender, 1);

    private async Task MoveBlockAsync(object? sender, int offset)
    {
        if (sender is Button { BindingContext: DynamicRecordBlockItem block })
        {
            await _viewModel.MoveBlockAsync(block, offset);
        }
    }

    private async void OnAddFieldClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: DynamicRecordBlockItem block })
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
        var selectedType = await DisplayActionSheetAsync("Type de l’objet", "Annuler", null, fieldTypes.Keys.ToArray());
        if (selectedType is null || !fieldTypes.TryGetValue(selectedType, out var fieldType))
        {
            return;
        }

        var label = await DisplayPromptAsync("Libellé de l’objet", "Libellé facultatif", "Ajouter", "Annuler");
        if (label is not null)
        {
            await _viewModel.AddFieldAsync(block, fieldType, label);
        }
    }

    private async void OnDeleteFieldClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: DynamicRecordFieldItem field })
        {
            return;
        }

        var block = _viewModel.FindBlock(field);
        if (block is null || !await DisplayAlertAsync("Supprimer l’objet", $"Supprimer « {field.Label} » ?", "Supprimer", "Annuler"))
        {
            return;
        }

        await _viewModel.DeleteFieldAsync(block, field);
    }

    private async void OnMoveFieldUpClicked(object? sender, EventArgs e) => await MoveFieldAsync(sender, -1);

    private async void OnMoveFieldDownClicked(object? sender, EventArgs e) => await MoveFieldAsync(sender, 1);

    private async Task MoveFieldAsync(object? sender, int offset)
    {
        if (sender is not Button { BindingContext: DynamicRecordFieldItem field })
        {
            return;
        }

        var block = _viewModel.FindBlock(field);
        if (block is not null)
        {
            await _viewModel.MoveFieldAsync(block, field, offset);
        }
    }

    private async void OnPickLocationClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: DynamicRecordFieldItem field })
        {
            await _viewModel.PickDynamicLocationAsync(field);
        }
    }

    private async void OnOpenMapClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: DynamicRecordFieldItem field })
        {
            await _viewModel.OpenDynamicMapAsync(field);
        }
    }

    private async void OnPickDocumentClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: DynamicRecordFieldItem field })
        {
            return;
        }

        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Sélectionner un document" });
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

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var isNewClient = !_viewModel.IsEditing;
        try
        {
            if (!_viewModel.ValidateForSave())
            {
                await DisplayAlertAsync("Vérifier les champs", "Vérifiez le prénom et les coordonnées avant d’enregistrer.", "OK");
                return;
            }

            if (isNewClient)
            {
                if (!await _viewModel.SaveAsync())
                {
                    await DisplayAlertAsync("Enregistrement impossible", "La fiche client n’a pas pu être enregistrée.", "OK");
                    return;
                }
            }
            else
            {
                await _viewModel.SaveStructureAsync();
                if (!await _viewModel.SaveDynamicAsync())
                {
                    await DisplayAlertAsync("Enregistrement impossible", "Les valeurs de la fiche n’ont pas pu être enregistrées.", "OK");
                    return;
                }

                if (!await _viewModel.SaveAsync())
                {
                    await DisplayAlertAsync("Vérifier les champs", "Vérifiez le prénom et les coordonnées avant d’enregistrer.", "OK");
                    return;
                }
            }

            await App.Services.GetRequiredService<Services.IAutomaticBackupService>().SaveAfterChangeAsync();

            if (isNewClient)
            {
                await DisplayAlertAsync(
                    "Sauvegarde recommandée",
                    "Une sauvegarde de vos clients a été créée. Utilisez l’action « Sauvegarder » et placez ce fichier sur un drive si possible pour ne pas le perdre.",
                    "Compris");
            }

            await Shell.Current.GoToAsync(isNewClient ? $"../ClientDetailsPage?id={_viewModel.ClientId}" : "..");
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Enregistrement impossible", exception.Message, "OK");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (_viewModel.IsStructureEditing && !await DisplayAlertAsync(
                "Quitter l’édition de structure",
                "Les changements de blocs et de champs sont appliqués au fur et à mesure. Les valeurs non enregistrées seront abandonnées. Continuer ?",
                "Quitter",
                "Rester"))
        {
            return;
        }

        _viewModel.CancelDraft();
        await Shell.Current.GoToAsync("..");
    }

}
