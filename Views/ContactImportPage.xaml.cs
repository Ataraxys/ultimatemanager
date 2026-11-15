using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using UltimateManager.Models;
using UltimateManager.Services;

namespace UltimateManager.Views;

public partial class ContactImportPage : ContentPage
{
    private readonly IContactImporter _importer;
    private readonly IClientService _clientService;

    public ObservableCollection<ImportContactItem> Contacts { get; } = [];

    public bool IsImporting { get; private set; }

    public bool IsNotBusy => !IsImporting;

    public ContactImportPage()
    {
        InitializeComponent();
        _importer = App.Services.GetRequiredService<IContactImporter>();
        _clientService = App.Services.GetRequiredService<IClientService>();
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Contacts.Count > 0)
        {
            return;
        }

        try
        {
            IsImporting = true;
            OnPropertyChanged(nameof(IsNotBusy));
            var contacts = await _importer.ImportAsync();
            foreach (var contact in contacts)
            {
                Contacts.Add(new ImportContactItem(contact));
            }
        }
        finally
        {
            IsImporting = false;
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    private void OnToggleAllClicked(object? sender, EventArgs e)
    {
        var select = Contacts.Any(contact => !contact.IsSelected);
        foreach (var contact in Contacts)
        {
            contact.IsSelected = select;
        }

        ContactsView.InvalidateMeasure();
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        if (IsImporting)
        {
            return;
        }

        var selectedContacts = Contacts.Where(contact => contact.IsSelected).Select(contact => contact.Client).ToList();
        if (selectedContacts.Count == 0)
        {
            await DisplayAlertAsync("Importation", "Sélectionnez au moins un contact.", "OK");
            return;
        }

        try
        {
            IsImporting = true;
            OnPropertyChanged(nameof(IsNotBusy));
            var existingClients = await _clientService.GetAllAsync();
            var importedCount = 0;
            foreach (var importedClient in selectedContacts)
            {
                var duplicate = FindDuplicate(existingClients, importedClient);
                if (duplicate is null)
                {
                    await _clientService.CreateAsync(importedClient);
                    existingClients = existingClients.Append(importedClient).ToList();
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
                await _clientService.UpdateAsync(duplicate);
                importedCount++;
            }

            await DisplayAlertAsync("Importation terminée", $"{importedCount} contact(s) traité(s).", "OK");
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            IsImporting = false;
            OnPropertyChanged(nameof(IsNotBusy));
        }
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
}
