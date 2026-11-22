using UltimateManager.Models.DynamicForms;
using UltimateManager.Services;
using UltimateManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UltimateManager.Views;

public partial class TemplatesPage : ContentPage
{
    private readonly TemplatesViewModel _viewModel;

    public TemplatesPage(TemplatesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.AddRequested += OnAddRequested;
        _viewModel.TemplateSelected += OnTemplateSelected;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnAddRequested(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Nouveau modèle", "Nom du modèle", "Créer", "Annuler", "Ex. Éleveur");
        if (!string.IsNullOrWhiteSpace(name))
        {
            await _viewModel.CreateAsync(name);
        }
    }

    private async void OnTemplateSelected(object? sender, EntityTemplate template)
    {
        await Shell.Current.GoToAsync($"TemplateEditorPage?id={template.Id}");
    }

    private async void OnTemplateTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { BindingContext: EntityTemplate template })
        {
            await _viewModel.SelectCommand.ExecuteAsync(template);
        }
    }

    private async void OnSetDefaultClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: EntityTemplate template })
        {
            return;
        }

        var service = App.Services.GetRequiredService<IDynamicFormService>();
        await service.SetDefaultTemplateAsync(template.Id);
        await DisplayAlertAsync("Template par défaut", $"« {template.Name} » sera utilisé pour les nouveaux clients.", "OK");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: EntityTemplate template } ||
            !await DisplayAlertAsync(
                "Supprimer le modèle",
                $"Supprimer « {template.Name} » ? Cette action est irréversible.",
                "Supprimer",
                "Annuler"))
        {
            return;
        }

        try
        {
            await _viewModel.DeleteAsync(template);
        }
        catch (InvalidOperationException exception)
        {
            await DisplayAlertAsync("Suppression impossible", exception.Message, "OK");
        }
    }
}
