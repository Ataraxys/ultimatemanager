using SQLite;

namespace UltimateManager.Services;

/// <summary>
/// Propriétaire de la connexion SQLite et du verrou d'accès de l'application.
/// Les migrations et les règles métier ne relèvent pas de ce composant.
/// </summary>
public sealed class SQLiteDatabaseCoordinator : ISqliteDatabase, IAsyncDisposable
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _databaseLock = new(1, 1);
    private readonly AsyncLocal<LockContext?> _lockContext = new();
    private readonly Lazy<Task> _initialization;
    private readonly Lazy<Task> _disposal;
    private readonly object _stateGate = new();
    private bool _disposed;

    public SQLiteDatabaseCoordinator()
        : this(Path.Combine(FileSystem.AppDataDirectory, "ultimate-manager.db3"))
    {
    }

    public SQLiteDatabaseCoordinator(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Le chemin de la base SQLite est requis.", nameof(databasePath));
        }

        DatabasePath = Path.GetFullPath(databasePath);
        var parentDirectory = Path.GetDirectoryName(DatabasePath)
            ?? throw new ArgumentException("Le chemin de la base SQLite est invalide.", nameof(databasePath));
        Directory.CreateDirectory(parentDirectory);

        _connection = new SQLiteAsyncConnection(DatabasePath);
        _initialization = new Lazy<Task>(InitializeCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        _disposal = new Lazy<Task>(DisposeCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string DatabasePath { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _initialization.Value.WaitAsync(cancellationToken);
        ThrowIfDisposed();
    }

    public async Task<T> ReadAsync<T>(
        Func<SQLiteAsyncConnection, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await InitializeAsync(cancellationToken);
        return await ExecuteLockedAsync(() => operation(_connection), cancellationToken);
    }

    public async Task ExecuteWriteAsync(
        Func<SQLiteAsyncConnection, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await InitializeAsync(cancellationToken);
        await ExecuteLockedAsync(() => operation(_connection), cancellationToken);
    }

    public async Task<T> ExecuteWriteAsync<T>(
        Func<SQLiteAsyncConnection, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await InitializeAsync(cancellationToken);
        return await ExecuteLockedAsync(() => operation(_connection), cancellationToken);
    }

    public async Task ExecuteTransactionAsync(
        Action<SQLiteConnection> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await InitializeAsync(cancellationToken);
        await ExecuteLockedAsync(() => _connection.RunInTransactionAsync(operation), cancellationToken);
    }

    /// <summary>
    /// Ferme définitivement la connexion. Un échec est partagé par les appels
    /// concurrents et propagé sans tentative de réouverture ou de retry.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        return new ValueTask(_disposal.Value);
    }

    private async Task InitializeCoreAsync()
    {
        await ExecuteLockedAsync(
            () => _connection.ExecuteScalarAsync<int>("PRAGMA user_version;"),
            CancellationToken.None);
    }

    private async Task DisposeCoreAsync()
    {
        await ExecuteLockedAsync(
            async () =>
            {
                lock (_stateGate)
                {
                    _disposed = true;
                }

                await _connection.CloseAsync();
            },
            CancellationToken.None,
            allowDisposed: true);
    }

    private async Task ExecuteLockedAsync(
        Func<Task> operation,
        CancellationToken cancellationToken,
        bool allowDisposed = false)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await EnterLockAsync(cancellationToken, allowDisposed);
        var lockHeld = false;
        try
        {
            MarkLockHeld();
            lockHeld = true;
            await operation();
        }
        catch (Exception operationException)
        {
            if (lockHeld)
            {
                lockHeld = false;
                try
                {
                    ExitLock();
                }
                catch (Exception releaseException)
                {
                    throw new AggregateException(
                        "L'opération SQLite et la libération du verrou ont échoué.",
                        operationException,
                        releaseException);
                }
            }

            throw;
        }
        finally
        {
            if (lockHeld)
            {
                lockHeld = false;
                ExitLock();
            }
        }
    }

    private async Task<T> ExecuteLockedAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken,
        bool allowDisposed = false)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await EnterLockAsync(cancellationToken, allowDisposed);
        var lockHeld = false;
        try
        {
            MarkLockHeld();
            lockHeld = true;
            return await operation();
        }
        catch (Exception operationException)
        {
            if (lockHeld)
            {
                lockHeld = false;
                try
                {
                    ExitLock();
                }
                catch (Exception releaseException)
                {
                    throw new AggregateException(
                        "L'opération SQLite et la libération du verrou ont échoué.",
                        operationException,
                        releaseException);
                }
            }

            throw;
        }
        finally
        {
            if (lockHeld)
            {
                lockHeld = false;
                ExitLock();
            }
        }
    }

    private async Task EnterLockAsync(CancellationToken cancellationToken, bool allowDisposed)
    {
        if (_lockContext.Value?.IsActive == true)
        {
            throw new InvalidOperationException(
                "Une acquisition imbriquée du verrou SQLite est interdite.");
        }

        await _databaseLock.WaitAsync(cancellationToken);
        try
        {
            lock (_stateGate)
            {
                if (_disposed && !allowDisposed)
                {
                    throw new ObjectDisposedException(nameof(SQLiteDatabaseCoordinator));
                }
            }
        }
        catch
        {
            _databaseLock.Release();
            throw;
        }
    }

    private void MarkLockHeld()
    {
        if (_lockContext.Value?.IsActive == true)
        {
            _databaseLock.Release();
            throw new InvalidOperationException(
                "Une acquisition imbriquée du verrou SQLite est interdite.");
        }

        _lockContext.Value = new LockContext();
    }

    private void ExitLock()
    {
        var context = _lockContext.Value;
        if (context is null || !context.IsActive)
        {
            throw new SynchronizationLockException(
                "Le verrou SQLite n'est pas détenu par l'opération courante.");
        }

        context.Deactivate();
        _lockContext.Value = null;
        _databaseLock.Release();
    }

    private void ThrowIfDisposed()
    {
        lock (_stateGate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SQLiteDatabaseCoordinator));
            }
        }
    }

    private sealed class LockContext
    {
        private int _isActive = 1;

        public bool IsActive => Volatile.Read(ref _isActive) == 1;

        public void Deactivate()
        {
            Volatile.Write(ref _isActive, 0);
        }
    }
}
