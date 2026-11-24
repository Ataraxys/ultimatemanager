using System.Text.Json;
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

    private async void OnTemplatesClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("TemplatesPage");
    }

    private async void OnImportRequested(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("ContactImportPage");
    }

    private async void OnShareBackupClicked(object? sender, EventArgs e)
    {
        try
        {
            var backupService = App.Services.GetRequiredService<ICompleteBackupService>();
            await backupService.CreateAndShareAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("Sauvegarde", $"La sauvegarde complète a échoué : {exception.Message}", "OK");
        }
    }

    private Task OnImportCompleteBackupClicked(object? sender, EventArgs e)
    {
        return DisplayAlertAsync("Restauration indisponible", ICompleteBackupService.RestoreUnavailableMessage, "OK");
    }

    private async void OnImportBackupClicked(object? sender, EventArgs e)
    {
        var backupService = App.Services.GetRequiredService<IClientBackupService>();
        IReadOnlyList<Client> importedClients;
        try
        {
            importedClients = await backupService.PickAndReadAsync();
        }
        catch (JsonException)
        {
            await DisplayAlertAsync("Importation", "Le fichier sélectionné n’est pas une sauvegarde valide.", "OK");
            return;
        }

        if (importedClients.Count == 0)
        {
            await DisplayAlertAsync("Importation", "Aucun client trouvé dans ce fichier.", "OK");
            return;
        }

        var clientService = App.Services.GetRequiredService<IClientService>();
        var existingClients = await clientService.GetAllAsync();
        var importedCount = 0;
        foreach (var importedClient in importedClients)
        {
            var duplicate = FindDuplicate(existingClients, importedClient);
            if (duplicate is null)
            {
                importedClient.Id = importedClient.Id == Guid.Empty ? Guid.NewGuid() : importedClient.Id;
                await clientService.CreateAsync(importedClient);
                existingClients = existingClients.Append(importedClient).ToList();
                importedCount++;
                continue;
            }

            if (!await DisplayAlertAsync("Client déjà présent", $"{importedClient.FullName} existe déjà. Remplacer ses informations ?", "Remplacer", "Ignorer"))
            {
                continue;
            }

            importedClient.Id = duplicate.Id;
            importedClient.UpdatedAt = DateTime.UtcNow;
            await clientService.UpdateAsync(importedClient);
            importedCount++;
        }

        await _viewModel.LoadCommand.ExecuteAsync(null);
        await DisplayAlertAsync("Importation terminée", $"{importedCount} client(s) traité(s).", "OK");
    }

    private static Client? FindDuplicate(IEnumerable<Client> clients, Client importedClient)
    {
        if (importedClient.Id != Guid.Empty)
        {
            var byId = clients.FirstOrDefault(client => client.Id == importedClient.Id);
            if (byId is not null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(importedClient.PhoneNumber))
        {
            var normalizedPhone = NormalizePhone(importedClient.PhoneNumber);
            return clients.FirstOrDefault(client => NormalizePhone(client.PhoneNumber) == normalizedPhone);
        }

        return clients.FirstOrDefault(client =>
            !string.IsNullOrWhiteSpace(importedClient.Email) &&
            string.Equals(client.Email, importedClient.Email, StringComparison.OrdinalIgnoreCase));
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
