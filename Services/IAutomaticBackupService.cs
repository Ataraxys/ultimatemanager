namespace UltimateManager.Services;

public interface IAutomaticBackupService
{
    Task SaveAfterChangeAsync(CancellationToken cancellationToken = default);
}
