using SQLite;
using UltimateManager.Models.DynamicForms;

namespace UltimateManager.Services;

public sealed class LocalWorkspaceContext
    : IWorkspaceContext
{
    private const string WorkspacePreferenceKey = "active-workspace-id";
    private readonly SQLiteAsyncConnection _connection;
    private readonly Task _initializationTask;
    private Guid _workspaceId;

    public LocalWorkspaceContext()
    {
        _connection = new SQLiteAsyncConnection(Path.Combine(FileSystem.AppDataDirectory, "ultimate-manager.db3"));
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
        await _connection.CreateTableAsync<Workspace>();
        try
        {
            await _connection.ExecuteAsync(
                "ALTER TABLE Workspaces ADD COLUMN DefaultTemplateId TEXT NOT NULL DEFAULT ''");
        }
        catch (SQLiteException)
        {
        }
        if (_workspaceId != Guid.Empty && await _connection.FindAsync<Workspace>(_workspaceId) is not null)
        {
            return;
        }

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Espace personnel",
            CreatedAt = DateTime.UtcNow
        };
        await _connection.InsertAsync(workspace);
        _workspaceId = workspace.Id;
        Preferences.Default.Set(WorkspacePreferenceKey, _workspaceId.ToString());
    }
}
