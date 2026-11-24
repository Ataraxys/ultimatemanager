using UltimateManager.Models;
using UltimateManager.Services;

internal sealed class ClientBackupStub : IClientBackupService
{
    public string BackupFilePath => Path.Combine(Path.GetTempPath(), "unused-client-backup.json");

    public int RegenerationCount { get; private set; }

    public IReadOnlyList<Client> LastClients { get; private set; } = [];

    public Task RegenerateAsync(IEnumerable<Client> clients, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RegenerationCount++;
        LastClients = clients.ToList();
        return Task.CompletedTask;
    }

    public Task ShareAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Client>> PickAndReadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Client>>([]);
    }
}
