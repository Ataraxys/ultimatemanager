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
