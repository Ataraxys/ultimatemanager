using UltimateManager.Models;

namespace UltimateManager.Services;

public interface IClientBackupService
{
    string BackupFilePath { get; }

    Task RegenerateAsync(IEnumerable<Client> clients, CancellationToken cancellationToken = default);

    Task ShareAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Client>> PickAndReadAsync(CancellationToken cancellationToken = default);
}
