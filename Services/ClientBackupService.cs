using System.Text.Json;
using UltimateManager.Models;

namespace UltimateManager.Services;

public sealed class ClientBackupService : IClientBackupService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string BackupFilePath => Path.Combine(FileSystem.AppDataDirectory, "clients-backup.json");

    public async Task RegenerateAsync(IEnumerable<Client> clients, CancellationToken cancellationToken = default)
    {
        var temporaryPath = BackupFilePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, clients, SerializerOptions, cancellationToken);
        }

        File.Move(temporaryPath, BackupFilePath, true);
    }

    public async Task ShareAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(BackupFilePath))
        {
            return;
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Sauvegarde des clients",
            File = new ShareFile(BackupFilePath)
        });
    }

    public async Task<IReadOnlyList<Client>> PickAndReadAsync(CancellationToken cancellationToken = default)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Sélectionner une sauvegarde de clients",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = ["application/json"],
                [DevicePlatform.iOS] = ["public.json"],
                [DevicePlatform.MacCatalyst] = ["public.json"],
                [DevicePlatform.WinUI] = [".json"]
            })
        });

        if (file is null)
        {
            return [];
        }

        await using var stream = await file.OpenReadAsync();
        return await JsonSerializer.DeserializeAsync<List<Client>>(stream, SerializerOptions, cancellationToken) ?? [];
    }
}
