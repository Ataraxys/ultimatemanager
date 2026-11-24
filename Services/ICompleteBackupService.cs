namespace UltimateManager.Services;

public interface ICompleteBackupService
{
    const string RestoreUnavailableMessage =
        "La restauration d’archives est temporairement désactivée afin de protéger les données existantes. Les sauvegardes peuvent toujours être créées et partagées.";

    string BackupFilePath { get; }

    Task CreateAsync(CancellationToken cancellationToken = default);

    Task CreateAndShareAsync(CancellationToken cancellationToken = default);

    Task<bool> PickAndRestoreAsync(CancellationToken cancellationToken = default);
}
