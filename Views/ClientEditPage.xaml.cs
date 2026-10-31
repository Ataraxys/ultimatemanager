using Microsoft.Extensions.DependencyInjection;
using UltimateManager.Models;
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

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("id", out var value) || !Guid.TryParse(value?.ToString(), out var id))
        {
            _viewModel.Load(null);
            return;
        }

        var clientService = App.Services.GetRequiredService<Services.IClientService>();
        var client = await clientService.GetByIdAsync(id);
        _viewModel.Load(client);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (await _viewModel.SaveAsync())
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

}
