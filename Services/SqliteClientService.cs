using SQLite;
using UltimateManager.Models;

namespace UltimateManager.Services;

public sealed class SqliteClientService : IClientService
{
    private readonly ISqliteDatabase _database;
    private readonly Lazy<Task> _initializationTask;
    private readonly IClientBackupService _backupService;

    public SqliteClientService(ISqliteDatabase database, IClientBackupService backupService)
    {
        _database = database;
        _backupService = backupService;
        _initializationTask = new Lazy<Task>(InitializeTablesAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _database.ReadAsync(connection => connection.Table<Client>()
            .OrderBy(client => client.LastName)
            .ThenBy(client => client.FirstName)
            .ToListAsync(), cancellationToken);
    }

    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _database.ReadAsync(connection => connection.FindAsync<Client>(id), cancellationToken);
    }

    public async Task<Client> CreateAsync(Client client, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _database.ExecuteWriteAsync(connection => connection.InsertAsync(client), cancellationToken);
        await RegenerateBackupAsync(cancellationToken);
        return client;
    }

    public async Task<Client> UpdateAsync(Client client, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _database.ExecuteWriteAsync(connection => connection.UpdateAsync(client), cancellationToken);
        await RegenerateBackupAsync(cancellationToken);
        return client;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _database.ExecuteWriteAsync(connection => connection.DeleteAsync<Client>(id), cancellationToken);
        await RegenerateBackupAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Client>> SearchAsync(string searchText, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var pattern = $"%{searchText.Trim()}%";

        return await _database.ReadAsync(connection => connection.QueryAsync<Client>(
            "SELECT * FROM Clients WHERE FirstName LIKE ? OR LastName LIKE ? OR City LIKE ? OR PhoneNumber LIKE ? ORDER BY LastName, FirstName",
            pattern,
            pattern,
            pattern,
            pattern), cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await _initializationTask.Value.WaitAsync(cancellationToken);
    }

    private async Task InitializeTablesAsync()
    {
        await _database.InitializeAsync();
        await _database.ExecuteWriteAsync(connection => connection.CreateTableAsync<Client>());
    }

    private async Task RegenerateBackupAsync(CancellationToken cancellationToken)
    {
        var clients = await _database.ReadAsync(
            connection => connection.Table<Client>().ToListAsync(),
            cancellationToken);
        await _backupService.RegenerateAsync(clients, cancellationToken);
    }
}
