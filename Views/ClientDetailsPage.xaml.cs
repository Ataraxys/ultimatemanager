using UltimateManager.Models.DynamicForms;
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

    private async void OnModifyClicked(object? sender, EventArgs e)
    {
        if (_clientId == Guid.Empty)
        {
            return;
        }

        await Shell.Current.GoToAsync($"ClientEditPage?id={_clientId}&mode=content");
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
        try
        {
            var templateId = await _viewModel.GetTemplateIdAsync();
            if (templateId == Guid.Empty)
            {
                await DisplayAlertAsync("Édition impossible", "Le template de cette fiche n’est pas disponible.", "OK");
                return;
            }

            await Shell.Current.GoToAsync($"TemplateEditorPage?id={templateId}&clientId={_clientId}&mode=structure");
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Édition impossible", exception.Message, "OK");
        }
    }

    private async void OnCancelEditClicked(object? sender, EventArgs e)
    {
        await _viewModel.CancelEditingAsync();
    }

    private async void OnSaveEditClicked(object? sender, EventArgs e)
    {
        await _viewModel.SaveEditingAsync();
    }

    private async void OnCallDynamicFieldClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: DynamicRecordFieldItem field })
        {
            await _viewModel.CallDynamicFieldAsync(field);
        }
    }

    private async void OnPickLocationClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: DynamicRecordFieldItem field })
        {
            await _viewModel.PickLocationAsync(field);
        }
    }

    private async void OnOpenMapClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: DynamicRecordFieldItem field })
        {
            await _viewModel.OpenInMapAsync(field);
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
