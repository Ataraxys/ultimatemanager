using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using UltimateManager.Models.DynamicForms;
using UltimateManager.Services;

namespace UltimateManager.ViewModels;

public sealed class TemplateEditorViewModel(
    IDynamicFormService formService,
    IAutomaticBackupService automaticBackupService) : ObservableObject
{
    private readonly IDynamicFormService _formService = formService;
    private readonly IAutomaticBackupService _automaticBackupService = automaticBackupService;

    public ObservableCollection<TemplateBlockEditorItem> Blocks { get; } = [];

    public Guid TemplateId { get; private set; }

    public string TemplateName { get; private set; } = string.Empty;

    public async Task LoadAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await _formService.GetTemplateAsync(templateId, cancellationToken)
            ?? throw new InvalidOperationException("Le modèle demandé n’existe pas.");
        TemplateId = template.Template.Id;
        TemplateName = template.Template.Name;
        Blocks.Clear();
        foreach (var block in template.Blocks)
        {
            Blocks.Add(new TemplateBlockEditorItem(block));
        }
        OnPropertyChanged(nameof(TemplateName));
    }

    public async Task AddBlockAsync(string name, CancellationToken cancellationToken = default)
    {
        var block = await _formService.AddBlockAsync(TemplateId, name, cancellationToken);
        Blocks.Add(new TemplateBlockEditorItem(new BlockDefinitionModel { Block = block }));
        RefreshBlockOrders();
    }

    public async Task AddFieldAsync(TemplateBlockEditorItem block, string label, FieldType fieldType, CancellationToken cancellationToken = default)
    {
        var field = await _formService.AddFieldAsync(block.Id, string.IsNullOrWhiteSpace(label) ? GetDefaultLabel(fieldType) : label, fieldType, cancellationToken);
        block.Fields.Add(new TemplateFieldEditorItem(field));
        RefreshFieldOrders(block);
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

    public async Task RenameBlockAsync(TemplateBlockEditorItem block, string name, CancellationToken cancellationToken = default)
    {
        block.Name = name.Trim();
        await _formService.UpdateBlockAsync(block.Model, cancellationToken);
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    public async Task RenameFieldAsync(TemplateFieldEditorItem field, string label, CancellationToken cancellationToken = default)
    {
        field.Label = label.Trim();
        await _formService.UpdateFieldAsync(field.Model, cancellationToken);
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    public async Task DeleteBlockAsync(TemplateBlockEditorItem block, CancellationToken cancellationToken = default)
    {
        await _formService.DeleteBlockAsync(block.Id, cancellationToken);
        Blocks.Remove(block);
        RefreshBlockOrders();
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    public async Task DeleteFieldAsync(TemplateFieldEditorItem field, TemplateBlockEditorItem block, CancellationToken cancellationToken = default)
    {
        await _formService.DeleteFieldAsync(field.Id, cancellationToken);
        block.Fields.Remove(field);
        RefreshFieldOrders(block);
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    public async Task MoveBlockAsync(TemplateBlockEditorItem block, int offset, CancellationToken cancellationToken = default)
    {
        await _formService.MoveBlockAsync(TemplateId, block.Id, offset, cancellationToken);
        var index = Blocks.IndexOf(block);
        var target = Math.Clamp(index + offset, 0, Blocks.Count - 1);
        if (index != target)
        {
            Blocks.Move(index, target);
        }
        RefreshBlockOrders();
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    public async Task MoveFieldAsync(TemplateFieldEditorItem field, TemplateBlockEditorItem block, int offset, CancellationToken cancellationToken = default)
    {
        await _formService.MoveFieldAsync(block.Id, field.Id, offset, cancellationToken);
        var index = block.Fields.IndexOf(field);
        var target = Math.Clamp(index + offset, 0, block.Fields.Count - 1);
        if (index != target)
        {
            block.Fields.Move(index, target);
        }
        RefreshFieldOrders(block);
        await _automaticBackupService.SaveAfterChangeAsync(cancellationToken);
    }

    private void RefreshBlockOrders()
    {
        for (var index = 0; index < Blocks.Count; index++)
        {
            Blocks[index].DisplayOrder = index;
        }
    }

    private static void RefreshFieldOrders(TemplateBlockEditorItem block)
    {
        for (var index = 0; index < block.Fields.Count; index++)
        {
            block.Fields[index].DisplayOrder = index;
        }
    }
}

public sealed class TemplateBlockEditorItem : ObservableObject
{
    public TemplateBlockEditorItem(BlockDefinitionModel model)
    {
        Model = model.Block;
        Fields = new ObservableCollection<TemplateFieldEditorItem>(model.Fields.Select(field => new TemplateFieldEditorItem(field)));
    }

    public BlockDefinition Model { get; }
    public ObservableCollection<TemplateFieldEditorItem> Fields { get; }
    public Guid Id => Model.Id;

    public string Name
    {
        get => Model.Name;
        set => SetProperty(Model.Name, value, Model, (item, name) => item.Name = name);
    }

    public int DisplayOrder
    {
        get => Model.DisplayOrder;
        set => SetProperty(Model.DisplayOrder, value, Model, (item, order) => item.DisplayOrder = order);
    }
}

public sealed class TemplateFieldEditorItem : ObservableObject
{
    public TemplateFieldEditorItem(FieldDefinition model) => Model = model;

    public FieldDefinition Model { get; }
    public Guid Id => Model.Id;

    public string Label
    {
        get => Model.Label;
        set => SetProperty(Model.Label, value, Model, (item, label) => item.Label = label);
    }

    public FieldType FieldType
    {
        get => Model.FieldType;
        set => SetProperty(Model.FieldType, value, Model, (item, type) => item.FieldType = type);
    }

    public int DisplayOrder
    {
        get => Model.DisplayOrder;
        set => SetProperty(Model.DisplayOrder, value, Model, (item, order) => item.DisplayOrder = order);
    }
}
