using UltimateManager.ViewModels;
using UltimateManager.Models;

namespace UltimateManager.Views;

public partial class ClientsPage : ContentPage
{
    private readonly ClientsViewModel _viewModel;

    public ClientsPage(ClientsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.ClientSelected += OnClientSelected;
        _viewModel.AddRequested += OnAddRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnAddRequested(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("ClientEditPage");
    }

    private async void OnClientSelected(object? sender, Client client)
    {
        await Shell.Current.GoToAsync($"ClientDetailsPage?id={client.Id}");
    }

    private async void OnClientTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { BindingContext: Client client })
        {
            await _viewModel.SelectCommand.ExecuteAsync(client);
        }
    }

    private async void OnDeleteInvoked(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: Client client } &&
            await DisplayAlertAsync("Supprimer le client", $"Supprimer {client.FullName} ?", "Supprimer", "Annuler"))
        {
            await _viewModel.DeleteCommand.ExecuteAsync(client);
        }
    }
}
