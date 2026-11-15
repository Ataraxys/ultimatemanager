using SQLite;
using UltimateManager.Models;

namespace UltimateManager.Services;

public sealed class SqliteClientService : IClientService
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly Task _initializationTask;
    private readonly IClientBackupService _backupService;

    public SqliteClientService(IClientBackupService backupService)
    {
        _backupService = backupService;
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "ultimate-manager.db3");
        _connection = new SQLiteAsyncConnection(databasePath);
        _initializationTask = _connection.CreateTableAsync<Client>();
    }

    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _connection.Table<Client>()
            .OrderBy(client => client.LastName)
            .ThenBy(client => client.FirstName)
            .ToListAsync();
    }

    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _connection.FindAsync<Client>(id);
    }

    public async Task<Client> CreateAsync(Client client, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _connection.InsertAsync(client);
        await RegenerateBackupAsync(cancellationToken);
        return client;
    }

    public async Task<Client> UpdateAsync(Client client, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _connection.UpdateAsync(client);
        await RegenerateBackupAsync(cancellationToken);
        return client;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _connection.DeleteAsync<Client>(id);
        await RegenerateBackupAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Client>> SearchAsync(string searchText, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var pattern = $"%{searchText.Trim()}%";

        return await _connection.QueryAsync<Client>(
            "SELECT * FROM Clients WHERE FirstName LIKE ? OR LastName LIKE ? OR City LIKE ? OR PhoneNumber LIKE ? ORDER BY LastName, FirstName",
            pattern,
            pattern,
            pattern,
            pattern);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await _initializationTask.WaitAsync(cancellationToken);
    }

    private async Task RegenerateBackupAsync(CancellationToken cancellationToken)
    {
        var clients = await _connection.Table<Client>().ToListAsync();
        await _backupService.RegenerateAsync(clients, cancellationToken);
    }
}
