using UltimateManager.Models.DynamicForms;
using UltimateManager.ViewModels;

namespace UltimateManager.Views;

public partial class TemplateEditorPage : ContentPage, IQueryAttributable
{
    private readonly TemplateEditorViewModel _viewModel;

    public TemplateEditorPage(TemplateEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var id))
        {
            await _viewModel.LoadAsync(id);
        }
    }

    private async void OnMoveFieldDownClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: TemplateFieldEditorItem field } && FindParentBlock(field) is { } block)
        {
            await _viewModel.MoveFieldAsync(field, block, 1);
        }
    }

    private async void OnAddBlockClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Nouveau bloc", "Nom du bloc", "Ajouter", "Annuler");
        if (!string.IsNullOrWhiteSpace(name))
        {
            await _viewModel.AddBlockAsync(name);
        }
    }

    private async void OnAddFieldClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: TemplateBlockEditorItem block })
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
        var selected = await DisplayActionSheet("Type d’élément", "Annuler", null, fieldTypes.Keys.ToArray());
        if (selected is null || !fieldTypes.TryGetValue(selected, out var fieldType))
        {
            return;
        }

        var label = await DisplayPromptAsync("Nouvel élément", "Libellé facultatif", "Ajouter", "Annuler");
        if (label is null)
        {
            return;
        }

        await _viewModel.AddFieldAsync(block, label, fieldType);
    }

    private async void OnBlockNameCompleted(object? sender, EventArgs e)
    {
        if (sender is Entry { BindingContext: TemplateBlockEditorItem block })
        {
            await _viewModel.RenameBlockAsync(block, block.Name);
        }
    }

    private async void OnFieldNameCompleted(object? sender, EventArgs e)
    {
        if (sender is Entry { BindingContext: TemplateFieldEditorItem field })
        {
            await _viewModel.RenameFieldAsync(field, field.Label);
        }
    }

    private async void OnDeleteBlockClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: TemplateBlockEditorItem block } &&
            await DisplayAlertAsync("Supprimer le bloc", $"Supprimer « {block.Name} » ?", "Supprimer", "Annuler"))
        {
            await _viewModel.DeleteBlockAsync(block);
        }
    }

    private async void OnDeleteFieldClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: TemplateFieldEditorItem field })
        {
            return;
        }

        var block = FindParentBlock(field);
        if (block is not null && await DisplayAlertAsync("Supprimer l’élément", $"Supprimer « {field.Label} » ?", "Supprimer", "Annuler"))
        {
            await _viewModel.DeleteFieldAsync(field, block);
        }
    }

    private async void OnMoveBlockUpClicked(object? sender, EventArgs e) => await MoveBlockAsync(sender, -1);
    private async void OnMoveBlockDownClicked(object? sender, EventArgs e) => await MoveBlockAsync(sender, 1);

    private async Task MoveBlockAsync(object? sender, int offset)
    {
        if (sender is Button { BindingContext: TemplateBlockEditorItem block })
        {
            await _viewModel.MoveBlockAsync(block, offset);
        }
    }

    private async void OnMoveFieldUpClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: TemplateFieldEditorItem field } && FindParentBlock(field) is { } block)
        {
            await _viewModel.MoveFieldAsync(field, block, -1);
        }
    }

    private TemplateBlockEditorItem? FindParentBlock(TemplateFieldEditorItem field) =>
        _viewModel.Blocks.FirstOrDefault(block => block.Fields.Contains(field));
}
