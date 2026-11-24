namespace UltimateManager.Services;

public interface IWorkspaceContext
{
    Guid WorkspaceId { get; }

    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}
