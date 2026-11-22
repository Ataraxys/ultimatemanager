using UltimateManager.Models;
using UltimateManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UltimateManager.Views;

public partial class ClientDetailsPage : ContentPage, IQueryAttributable
{
    private readonly ClientDetailsViewModel _viewModel;
    private Guid _clientId;

    public ClientDetailsPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<ClientDetailsViewModel>();
        BindingContext = _viewModel;
    }

    private async void OnDynamicEditClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"ClientDynamicRecordPage?id={_clientId}");
    }

    private async void OnEditTemplateClicked(object? sender, EventArgs e)
    {
        var service = App.Services.GetRequiredService<Services.IDynamicFormService>();
        var record = await service.GetRecordForExternalEntityAsync(_clientId);
        if (record is null)
        {
            var template = await service.GetDefaultClientTemplateAsync();
            record = await service.GetOrCreateRecordAsync(_clientId, template.Template.Id);
        }

        await Shell.Current.GoToAsync($"TemplateEditorPage?id={record.Record.TemplateId}");
    }

    private async void OnCloneTemplateClicked(object? sender, EventArgs e)
    {
        var service = App.Services.GetRequiredService<Services.IDynamicFormService>();
        var record = await service.GetRecordForExternalEntityAsync(_clientId);
        if (record is null)
        {
            var template = await service.GetDefaultClientTemplateAsync();
            record = await service.GetOrCreateRecordAsync(_clientId, template.Template.Id);
        }

        var name = await DisplayPromptAsync(
            "Nouveau template",
            "Nom du template",
            "Créer",
            "Annuler",
            $"Copie de {record.Record.TemplateId}");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var clone = await service.CloneTemplateAsync(record.Record.TemplateId, name);
        await Shell.Current.GoToAsync($"TemplateEditorPage?id={clone.Template.Id}");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && Guid.TryParse(value?.ToString(), out var id))
        {
            _clientId = id;
            _ = LoadAsync();
        }
    }

    private Task LoadAsync() => _viewModel.LoadAsync(_clientId);

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"ClientEditPage?id={_clientId}");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_viewModel.CurrentClient is null ||
            !await DisplayAlertAsync("Supprimer le client", $"Supprimer {_viewModel.CurrentClient.FullName} ?", "Supprimer", "Annuler"))
        {
            return;
        }

        var service = App.Services.GetRequiredService<Services.IClientService>();
        await service.DeleteAsync(_clientId);
        await Shell.Current.GoToAsync("..");
    }
}
