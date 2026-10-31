using Microsoft.Maui.ApplicationModel.Communication;
using UltimateManager.Models;
using MauiContacts = Microsoft.Maui.ApplicationModel.Communication.Contacts;

namespace UltimateManager.Services;

public sealed class ContactImporter : IContactImporter
{
    public async Task<IReadOnlyList<Client>> ImportAsync(CancellationToken cancellationToken = default)
    {
        var permission = await Permissions.RequestAsync<Permissions.ContactsRead>();
        if (permission != PermissionStatus.Granted)
        {
            return [];
        }

        var contacts = await MauiContacts.Default.GetAllAsync(cancellationToken);
        var imported = new List<Client>();

        foreach (var contact in contacts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var phone = contact.Phones.FirstOrDefault()?.PhoneNumber?.Trim() ?? string.Empty;
            var email = contact.Emails.FirstOrDefault()?.EmailAddress?.Trim();
            var displayName = contact.DisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(displayName) || (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email)))
            {
                continue;
            }

            var nameParts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            imported.Add(new Client
            {
                Id = Guid.NewGuid(),
                FirstName = nameParts.FirstOrDefault() ?? displayName,
                LastName = nameParts.Length > 1 ? nameParts[1] : string.Empty,
                PhoneNumber = phone,
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        return imported;
    }
}