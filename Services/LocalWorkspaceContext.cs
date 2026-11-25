using UltimateManager.Models.DynamicForms;

namespace UltimateManager.Services;

public sealed class LocalWorkspaceContext
    : IWorkspaceContext
{
    private const string WorkspacePreferenceKey = "active-workspace-id";
    private readonly ISqliteDatabase _database;
    private readonly Task _initializationTask;
    private Guid _workspaceId;

    public LocalWorkspaceContext(ISqliteDatabase database)
    {
        _database = database;
        _workspaceId = Guid.TryParse(
            Preferences.Default.Get(WorkspacePreferenceKey, string.Empty),
            out var workspaceId)
            ? workspaceId
            : Guid.Empty;
        _initializationTask = InitializeAsync();
    }

    public Guid WorkspaceId => _workspaceId;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await _initializationTask.WaitAsync(cancellationToken);
    }

    private async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await _database.ExecuteWriteAsync(connection => connection.CreateTableAsync<Workspace>());
        var workspaceColumns = await _database.ReadAsync(connection => connection.GetTableInfoAsync("Workspaces"));
        if (workspaceColumns.All(column => column.Name != nameof(Workspace.DefaultTemplateId)))
        {
            await _database.ExecuteWriteAsync(connection => connection.ExecuteAsync(
                "ALTER TABLE Workspaces ADD COLUMN DefaultTemplateId TEXT NOT NULL DEFAULT ''"));
        }

        var workspace = await _database.ExecuteWriteAsync(async connection =>
        {
            var active = _workspaceId == Guid.Empty
                ? null
                : await connection.FindAsync<Workspace>(_workspaceId);
            if (active is not null)
            {
                return active;
            }

            var existing = await connection.Table<Workspace>()
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefaultAsync();
            if (existing is not null)
            {
                return existing;
            }

            var created = new Workspace
            {
                Id = Guid.NewGuid(),
                Name = "Espace personnel",
                CreatedAt = DateTime.UtcNow
            };
            await connection.InsertAsync(created);
            return created;
        });
        _workspaceId = workspace.Id;
        Preferences.Default.Set(WorkspacePreferenceKey, _workspaceId.ToString());
    }
}
