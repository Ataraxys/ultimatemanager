using SQLite;
using UltimateManager.Models;
using UltimateManager.Models.DynamicForms;

namespace UltimateManager.Services;

public sealed class SqliteClientService : IClientService
{
    private readonly ISqliteDatabase _database;
    private readonly IWorkspaceContext _workspaceContext;
    private readonly Lazy<Task> _initializationTask;
    private readonly IClientBackupService _backupService;

    public SqliteClientService(
        ISqliteDatabase database,
        IWorkspaceContext workspaceContext,
        IClientBackupService backupService)
    {
        _database = database;
        _workspaceContext = workspaceContext;
        _backupService = backupService;
        _initializationTask = new Lazy<Task>(InitializeTablesAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _database.ReadAsync(connection => connection.Table<Client>()
            .Where(client => client.WorkspaceId == _workspaceContext.WorkspaceId)
            .OrderBy(client => client.LastName)
            .ThenBy(client => client.FirstName)
            .ToListAsync(), cancellationToken);
    }

    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _database.ReadAsync(async connection =>
        {
            var client = await connection.FindAsync<Client>(id);
            return client?.WorkspaceId == _workspaceContext.WorkspaceId ? client : null;
        }, cancellationToken);
    }

    public async Task<Client> CreateAsync(Client client, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        client.WorkspaceId = _workspaceContext.WorkspaceId;
        await _database.ExecuteWriteAsync(connection => connection.InsertAsync(client), cancellationToken);
        await RegenerateBackupAsync(cancellationToken);
        return client;
    }

    public async Task<Client> UpdateAsync(Client client, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _database.ExecuteTransactionAsync(connection =>
        {
            var existing = connection.Find<Client>(client.Id);
            if (existing is null || existing.WorkspaceId != _workspaceContext.WorkspaceId)
            {
                throw new InvalidOperationException("Le client n’existe pas dans l’espace de travail actif.");
            }

            client.WorkspaceId = existing.WorkspaceId;
            client.CreatedAt = existing.CreatedAt;
            connection.Update(client);
        }, cancellationToken);
        await RegenerateBackupAsync(cancellationToken);
        return client;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _database.ExecuteTransactionAsync(connection =>
        {
            var client = connection.Find<Client>(id);
            if (client is null || client.WorkspaceId != _workspaceContext.WorkspaceId)
            {
                return;
            }

            var records = connection.Table<EntityRecord>()
                .Where(record => record.WorkspaceId == _workspaceContext.WorkspaceId &&
                                 record.ExternalEntityId == client.Id)
                .ToList();
            var templateIds = records.Select(record => record.TemplateId).Distinct().ToList();
            foreach (var record in records)
            {
                connection.Execute("DELETE FROM FieldValues WHERE EntityRecordId = ?", record.Id);
                connection.Execute("DELETE FROM BlockInstances WHERE EntityRecordId = ?", record.Id);
                connection.Delete(record);
            }

            var privateTemplateName = $"Fiche de {client.Id:N}";
            var privateTemplates = connection.Table<EntityTemplate>()
                .Where(template => template.WorkspaceId == _workspaceContext.WorkspaceId &&
                                   (template.OwnerClientId == client.Id ||
                                    (template.OwnerClientId == null && template.Name == privateTemplateName)))
                .ToList();
            foreach (var template in privateTemplates.Where(template =>
                         template.OwnerClientId == client.Id || templateIds.Contains(template.Id)))
            {
                if (connection.Table<EntityRecord>().Any(record => record.TemplateId == template.Id))
                {
                    continue;
                }

                DeleteTemplateDefinitions(connection, template.Id);
                connection.Delete(template);
            }

            connection.Delete(client);
        }, cancellationToken);
        await RegenerateBackupAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Client>> SearchAsync(string searchText, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var pattern = $"%{searchText.Trim()}%";

        return await _database.ReadAsync(connection => connection.QueryAsync<Client>(
            "SELECT * FROM Clients WHERE WorkspaceId = ? AND (FirstName LIKE ? OR LastName LIKE ? OR City LIKE ? OR PhoneNumber LIKE ?) ORDER BY LastName, FirstName",
            _workspaceContext.WorkspaceId,
            pattern,
            pattern,
            pattern,
            pattern), cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await _workspaceContext.EnsureInitializedAsync(cancellationToken);
        await _initializationTask.Value.WaitAsync(cancellationToken);
    }

    private async Task InitializeTablesAsync()
    {
        await _database.InitializeAsync();
        var clientsTableExists = await _database.ReadAsync(async connection =>
            await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Clients'") == 1);
        if (clientsTableExists)
        {
            var columns = await _database.ReadAsync(connection => connection.GetTableInfoAsync("Clients"));
            if (columns.All(column => column.Name != nameof(Client.WorkspaceId)))
            {
                await _database.ExecuteWriteAsync(connection => connection.ExecuteAsync(
                    "ALTER TABLE Clients ADD COLUMN WorkspaceId TEXT NOT NULL DEFAULT ''"));
            }
        }

        await _database.ExecuteWriteAsync(connection => connection.CreateTableAsync<Client>());
        await _database.ExecuteWriteAsync(connection => connection.ExecuteAsync(
            "UPDATE Clients SET WorkspaceId = ? WHERE WorkspaceId IS NULL OR WorkspaceId = '' OR WorkspaceId = ?",
            _workspaceContext.WorkspaceId,
            Guid.Empty));
    }

    private async Task RegenerateBackupAsync(CancellationToken cancellationToken)
    {
        var clients = await _database.ReadAsync(
            connection => connection.Table<Client>()
                .Where(client => client.WorkspaceId == _workspaceContext.WorkspaceId)
                .ToListAsync(),
            cancellationToken);
        await _backupService.RegenerateAsync(clients, cancellationToken);
    }

    private static void DeleteTemplateDefinitions(SQLiteConnection connection, Guid templateId)
    {
        var blocks = connection.Table<BlockDefinition>()
            .Where(block => block.TemplateId == templateId)
            .ToList();
        foreach (var block in blocks)
        {
            var fieldIds = connection.Table<FieldDefinition>()
                .Where(field => field.BlockDefinitionId == block.Id)
                .ToList()
                .Select(field => field.Id)
                .ToArray();
            foreach (var fieldId in fieldIds)
            {
                connection.Execute("DELETE FROM FieldValues WHERE FieldDefinitionId = ?", fieldId);
            }

            connection.Execute("DELETE FROM FieldDefinitions WHERE BlockDefinitionId = ?", block.Id);
            connection.Delete(block);
        }
    }
}
