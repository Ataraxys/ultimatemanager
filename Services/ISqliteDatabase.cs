using SQLite;

namespace UltimateManager.Services;

/// <summary>
/// Point d'accès technique centralisé à une base SQLite.
/// Ce contrat ne contient aucune règle métier et n'expose aucun modèle métier.
///
/// Les callbacks de lecture et d'écriture sont exécutés sous le verrou du
/// coordinateur. Ils ne doivent pas appeler une autre opération du même
/// coordinateur et ne doivent pas conserver la connexion après leur retour.
/// Le callback transactionnel est strictement synchrone : il est interdit
/// d'y utiliser Wait(), Result ou GetAwaiter().GetResult() pour contourner
/// cette contrainte.
/// Après DisposeAsync, le coordinateur est définitivement inutilisable.
/// Un échec de fermeture est propagé et ne déclenche aucune réouverture ni
/// nouvelle tentative automatique ; une ressource native peut rester non
/// libérée dans ce cas.
/// </summary>
public interface ISqliteDatabase
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute une lecture sous le verrou du coordinateur.
    /// </summary>
    Task<T> ReadAsync<T>(
        Func<SQLiteAsyncConnection, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute une écriture sous le verrou du coordinateur.
    /// </summary>
    Task ExecuteWriteAsync(
        Func<SQLiteAsyncConnection, Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute une écriture sous le verrou du coordinateur et retourne son résultat.
    /// </summary>
    Task<T> ExecuteWriteAsync<T>(
        Func<SQLiteAsyncConnection, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute une transaction avec un callback SQLite strictement synchrone.
    /// Les opérations asynchrones ou les appels imbriqués au coordinateur sont interdits.
    /// </summary>
    Task ExecuteTransactionAsync(
        Action<SQLiteConnection> operation,
        CancellationToken cancellationToken = default);
}
