using UltimateManager.ViewModels;
using UltimateManager.Models;
using Microsoft.Extensions.DependencyInjection;
using UltimateManager.Services;

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
        _viewModel.ImportRequested += OnImportRequested;
    }

    private async void OnImportRequested(object? sender, EventArgs e)
    {
        var importer = App.Services.GetRequiredService<IContactImporter>();
        var clientService = App.Services.GetRequiredService<IClientService>();
        var importedClients = await importer.ImportAsync();
        if (importedClients.Count == 0)
        {
            await DisplayAlertAsync("Importation", "Aucun contact à importer ou permission refusée.", "OK");
            return;
        }

        var existingClients = await clientService.GetAllAsync();
        var importedCount = 0;
        foreach (var importedClient in importedClients)
        {
            var duplicate = FindDuplicate(existingClients, importedClient);
            if (duplicate is null)
            {
                await clientService.CreateAsync(importedClient);
                importedCount++;
                continue;
            }

            var update = await DisplayAlertAsync(
                "Contact déjà présent",
                $"{importedClient.FullName} existe déjà. Mettre à jour ses coordonnées ?",
                "Mettre à jour",
                "Ignorer");
            if (!update)
            {
                continue;
            }

            duplicate.FirstName = importedClient.FirstName;
            duplicate.LastName = importedClient.LastName;
            if (!string.IsNullOrWhiteSpace(importedClient.PhoneNumber))
            {
                duplicate.PhoneNumber = importedClient.PhoneNumber;
            }

            if (!string.IsNullOrWhiteSpace(importedClient.Email))
            {
                duplicate.Email = importedClient.Email;
            }

            duplicate.UpdatedAt = DateTime.UtcNow;
            await clientService.UpdateAsync(duplicate);
            importedCount++;
        }

        await _viewModel.LoadCommand.ExecuteAsync(null);
        await DisplayAlertAsync("Importation terminée", $"{importedCount} contact(s) traité(s).", "OK");
    }

    private static Client? FindDuplicate(IEnumerable<Client> clients, Client importedClient)
    {
        if (string.IsNullOrWhiteSpace(importedClient.PhoneNumber))
        {
            return clients.FirstOrDefault(client =>
                !string.IsNullOrWhiteSpace(importedClient.Email) &&
                string.Equals(client.Email, importedClient.Email, StringComparison.OrdinalIgnoreCase));
        }

        var normalizedPhone = NormalizePhone(importedClient.PhoneNumber);
        return clients.FirstOrDefault(client => NormalizePhone(client.PhoneNumber) == normalizedPhone);
    }

    private static string NormalizePhone(string phoneNumber) =>
        new(phoneNumber.Where(char.IsDigit).ToArray());

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
