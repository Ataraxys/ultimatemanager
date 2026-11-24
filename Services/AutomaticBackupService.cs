namespace UltimateManager.Services;

public sealed class AutomaticBackupService(ICompleteBackupService completeBackupService) : IAutomaticBackupService
{
    private const string FirstSharePreferenceKey = "complete-backup-first-share-offered";
    private readonly ICompleteBackupService _completeBackupService = completeBackupService;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task SaveAfterChangeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await _completeBackupService.CreateAsync(cancellationToken);
            if (Preferences.Default.Get(FirstSharePreferenceKey, false))
            {
                return;
            }

            Preferences.Default.Set(FirstSharePreferenceKey, true);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Choisir l’emplacement de la sauvegarde UltimateManager",
                File = new ShareFile(_completeBackupService.BackupFilePath)
            });
        }
        finally
        {
            _gate.Release();
        }
    }
}
