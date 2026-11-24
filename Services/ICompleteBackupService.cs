namespace UltimateManager.Services;

public interface ICompleteBackupService
{
    string BackupFilePath { get; }

    Task CreateAsync(CancellationToken cancellationToken = default);

    Task CreateAndShareAsync(CancellationToken cancellationToken = default);

    Task<bool> PickAndRestoreAsync(CancellationToken cancellationToken = default);
}
