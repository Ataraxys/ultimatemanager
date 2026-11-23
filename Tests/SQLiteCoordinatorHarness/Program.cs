using SQLitePCL;
using UltimateManager.Models;
using UltimateManager.Services;

raw.SetProvider(new SQLite3Provider_e_sqlite3());

var tests = new (string Name, Func<Task> Test)[]
{
    ("01_INIT_CONCURRENT", TestConcurrentInitializationAsync),
    ("02_CONCURRENT_WRITES", TestConcurrentWritesAsync),
    ("03_TRANSACTION_ROLLBACK", TestTransactionRollbackAsync),
    ("04_REUSE_AFTER_EXCEPTION", TestReuseAfterExceptionAsync),
    ("05_NESTED_OPERATION_REJECTED", TestNestedOperationAsync),
    ("06_LOCK_CANCELLATION", TestLockCancellationAsync),
    ("07_CHILD_DURING_PARENT_REJECTED", TestChildDuringParentAsync),
    ("08_CHILD_AFTER_PARENT_RELEASE", TestChildAfterParentReleaseAsync),
    ("09_DISPOSE_DURING_WRITE", TestDisposeDuringWriteAsync),
    ("10_CONCURRENT_DISPOSE", TestConcurrentDisposeAsync),
    ("11_OPERATION_AFTER_DISPOSE", TestOperationAfterDisposeAsync),
    ("12_INITIALIZE_AFTER_DISPOSE", TestInitializeAfterDisposeAsync),
    ("13_INITIALIZATION_FAILURE_STICKY", TestInitializationFailureAsync),
    ("14_DISPOSE_AFTER_INITIALIZATION_FAILURE", TestDisposeAfterInitializationFailureAsync),
    ("15_PARENT_DIRECTORY_CREATION", TestParentDirectoryCreationAsync),
    ("16_CLOSE_FAILURE_INJECTION", TestCloseFailureInjectionAsync),
    ("17_RELEASE_FAILURE_INJECTION", TestReleaseFailureInjectionAsync),
    ("18_CLIENT_TABLE_INITIALIZATION", TestClientTableInitializationAsync),
    ("19_CLIENT_INITIALIZATION_IDEMPOTENT", TestClientInitializationIdempotentAsync),
    ("20_CLIENT_CREATE_READ", TestClientCreateReadAsync),
    ("21_CLIENT_UPDATE", TestClientUpdateAsync),
    ("22_CLIENT_DELETE", TestClientDeleteAsync),
    ("23_CLIENT_SEARCH", TestClientSearchAsync),
    ("24_CLIENT_CONCURRENT_OPERATIONS", TestClientConcurrentOperationsAsync),
    ("25_CLIENT_REUSE_AFTER_EXCEPTION", TestClientReuseAfterExceptionAsync),
    ("26_CLIENT_NO_INDEPENDENT_CONNECTION", TestClientNoIndependentConnectionAsync),
    ("27_CLIENT_BACKUP_REGENERATION", TestClientBackupRegenerationAsync),
    ("28_CLIENT_HISTORICAL_DATA", TestClientHistoricalDataAsync)
};

var failed = 0;
foreach (var (name, test) in tests)
{
    try
    {
        await test();
        Console.WriteLine($"PASS {name}");
    }
    catch (NotSupportedException exception)
    {
        Console.WriteLine($"NOT_TESTED {name}: {exception.Message}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {exception.GetType().Name}: {exception.Message}");
    }
}

Console.WriteLine($"RESULT failed={failed}");
return failed == 0 ? 0 : 1;

static async Task TestConcurrentInitializationAsync()
{
    await WithCoordinatorAsync(async coordinator =>
    {
        await Task.WhenAll(
            Enumerable.Range(0, 2).Select(_ => coordinator.InitializeAsync()));
    });
}

static async Task TestConcurrentWritesAsync()
{
    await WithCoordinatorAsync(async coordinator =>
    {
        await coordinator.ExecuteWriteAsync(connection =>
            connection.ExecuteAsync("CREATE TABLE Items (Id INTEGER PRIMARY KEY, Value INTEGER NOT NULL);"));

        await Task.WhenAll(Enumerable.Range(1, 25).Select(value =>
            coordinator.ExecuteWriteAsync(connection =>
                connection.ExecuteAsync("INSERT INTO Items (Value) VALUES (?);", value))));

        var count = await coordinator.ReadAsync(connection =>
            connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Items;"));
        Assert(count == 25, $"25 écritures attendues, {count} trouvées.");
    });
}

static async Task TestTransactionRollbackAsync()
{
    await WithCoordinatorAsync(async coordinator =>
    {
        await coordinator.ExecuteWriteAsync(connection =>
            connection.ExecuteAsync("CREATE TABLE Items (Id INTEGER PRIMARY KEY, Value INTEGER NOT NULL);"));

        await coordinator.ExecuteTransactionAsync(transaction =>
        {
            transaction.Execute("INSERT INTO Items (Value) VALUES (?);", 1);
            transaction.Execute("INSERT INTO Items (Value) VALUES (?);", 2);
        });

        try
        {
            await coordinator.ExecuteTransactionAsync(transaction =>
            {
                transaction.Execute("INSERT INTO Items (Value) VALUES (?);", 3);
                throw new InvalidOperationException("rollback attendu");
            });
        }
        catch (InvalidOperationException)
        {
        }

        var count = await coordinator.ReadAsync(connection =>
            connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Items WHERE Value = 3;"));
        Assert(count == 0, "La ligne de la transaction en échec existe encore.");
    });
}

static async Task TestReuseAfterExceptionAsync()
{
    await WithCoordinatorAsync(async coordinator =>
    {
        try
        {
            await coordinator.ExecuteWriteAsync(_ => throw new InvalidOperationException("erreur attendue"));
        }
        catch (InvalidOperationException)
        {
        }

        await coordinator.ExecuteWriteAsync(connection =>
            connection.ExecuteAsync("CREATE TABLE Reuse (Value INTEGER NOT NULL);"));
    });
}

static async Task TestNestedOperationAsync()
{
    await WithCoordinatorAsync(async coordinator =>
    {
        var rejected = false;
        try
        {
            await coordinator.ExecuteWriteAsync(_ =>
                coordinator.ExecuteWriteAsync(__ => Task.CompletedTask));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Assert(rejected, "L'acquisition imbriquée n'a pas été refusée.");
    });
}

static async Task TestLockCancellationAsync()
{
    await WithCoordinatorAsync(async coordinator =>
    {
        var entered = NewSignal();
        var release = NewSignal();
        var holding = coordinator.ExecuteWriteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
        });

        await entered.Task;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var cancelled = false;
        try
        {
            await coordinator.ExecuteWriteAsync(_ => Task.CompletedTask, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        release.SetResult();
        await holding;
        Assert(cancelled, "L'attente du verrou n'a pas été annulée.");
    });
}

static async Task TestChildDuringParentAsync()
{
    await WithCoordinatorAsync(async coordinator =>
    {
        var childRejected = false;
        await coordinator.ExecuteWriteAsync(async _ =>
        {
            var child = Task.Run(async () =>
            {
                try
                {
                    await coordinator.ExecuteWriteAsync(__ => Task.CompletedTask);
                }
                catch (InvalidOperationException)
                {
                    childRejected = true;
                }
            });

            await child;
        });

        Assert(childRejected, "La tâche enfant n'a pas été refusée pendant le verrou parent.");
    });
}

static async Task TestChildAfterParentReleaseAsync()
{
    await WithCoordinatorAsync(async coordinator =>
    {
        var childMayContinue = NewSignal();
        Task child = Task.CompletedTask;

        await coordinator.ExecuteWriteAsync(_ =>
        {
            child = Task.Run(async () =>
            {
                await childMayContinue.Task;
                await coordinator.ExecuteWriteAsync(__ => Task.CompletedTask);
            });
            return Task.CompletedTask;
        });

        childMayContinue.SetResult();
        await child;
    });
}

static async Task TestDisposeDuringWriteAsync()
{
    var root = CreateTemporaryDirectory();
    var coordinator = new SQLiteDatabaseCoordinator(Path.Combine(root, "database.db3"));
    try
    {
        var entered = NewSignal();
        var release = NewSignal();
        var write = coordinator.ExecuteWriteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
        });

        await entered.Task;
        var dispose = coordinator.DisposeAsync().AsTask();
        await Task.Delay(50);
        Assert(!dispose.IsCompleted, "DisposeAsync n'a pas attendu l'écriture active.");

        release.SetResult();
        await write;
        await dispose;
    }
    finally
    {
        try { await coordinator.DisposeAsync(); } catch { }
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestConcurrentDisposeAsync()
{
    var root = CreateTemporaryDirectory();
    var coordinator = new SQLiteDatabaseCoordinator(Path.Combine(root, "database.db3"));
    try
    {
        var first = coordinator.DisposeAsync().AsTask();
        var second = coordinator.DisposeAsync().AsTask();
        await Task.WhenAll(first, second);
    }
    finally
    {
        try { await coordinator.DisposeAsync(); } catch { }
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestOperationAfterDisposeAsync()
{
    var root = CreateTemporaryDirectory();
    var coordinator = new SQLiteDatabaseCoordinator(Path.Combine(root, "database.db3"));
    try
    {
        await coordinator.DisposeAsync();
        await AssertThrowsAsync<ObjectDisposedException>(() =>
            coordinator.ExecuteWriteAsync(_ => Task.CompletedTask));
    }
    finally
    {
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestInitializeAfterDisposeAsync()
{
    var root = CreateTemporaryDirectory();
    var coordinator = new SQLiteDatabaseCoordinator(Path.Combine(root, "database.db3"));
    try
    {
        await coordinator.DisposeAsync();
        await AssertThrowsAsync<ObjectDisposedException>(() => coordinator.InitializeAsync());
    }
    finally
    {
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestInitializationFailureAsync()
{
    var root = CreateTemporaryDirectory();
    var coordinator = new SQLiteDatabaseCoordinator(root);
    try
    {
        var first = await CaptureExceptionAsync(() => coordinator.InitializeAsync());
        var second = await CaptureExceptionAsync(() => coordinator.InitializeAsync());
        Assert(first.GetType() == second.GetType(), "L'échec d'initialisation n'est pas stable.");
    }
    finally
    {
        try { await coordinator.DisposeAsync(); } catch { }
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestDisposeAfterInitializationFailureAsync()
{
    var root = CreateTemporaryDirectory();
    var coordinator = new SQLiteDatabaseCoordinator(root);
    try
    {
        try { await coordinator.InitializeAsync(); } catch { }
        await coordinator.DisposeAsync();
    }
    finally
    {
        try { await coordinator.DisposeAsync(); } catch { }
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestParentDirectoryCreationAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "um-parent-" + Guid.NewGuid().ToString("N"));
    var databasePath = Path.Combine(root, "missing", "database.db3");
    var coordinator = new SQLiteDatabaseCoordinator(databasePath);
    try
    {
        await coordinator.InitializeAsync();
        Assert(Directory.Exists(Path.GetDirectoryName(databasePath)!), "Le répertoire parent n'existe pas.");
    }
    finally
    {
        await coordinator.DisposeAsync();
        DeleteTemporaryDirectory(root);
    }
}

static Task TestCloseFailureInjectionAsync()
{
    throw new NotSupportedException("CloseAsync n'est pas injectable sans seam de test.");
}

static Task TestReleaseFailureInjectionAsync()
{
    throw new NotSupportedException("La libération interne n'est pas injectable sans réflexion ou seam dédié.");
}

static async Task TestClientTableInitializationAsync()
{
    await WithClientServiceAsync(async (_, service, _) =>
    {
        await service.GetAllAsync();
    });
}

static async Task TestClientInitializationIdempotentAsync()
{
    await WithClientServiceAsync(async (_, service, _) =>
    {
        await Task.WhenAll(service.GetAllAsync(), service.GetAllAsync(), service.SearchAsync("client"));
    });
}

static async Task TestClientCreateReadAsync()
{
    await WithClientServiceAsync(async (_, service, _) =>
    {
        var client = CreateClient("Ada", "Lovelace");
        await service.CreateAsync(client);
        var result = await service.GetByIdAsync(client.Id);
        Assert(result?.Id == client.Id && result.FirstName == "Ada", "Le client créé n'est pas relu correctement.");
    });
}

static async Task TestClientUpdateAsync()
{
    await WithClientServiceAsync(async (_, service, _) =>
    {
        var client = CreateClient("Grace", "Hopper");
        await service.CreateAsync(client);
        client.City = "Arlington";
        await service.UpdateAsync(client);
        var result = await service.GetByIdAsync(client.Id);
        Assert(result?.City == "Arlington", "La modification du client n'est pas persistée.");
    });
}

static async Task TestClientDeleteAsync()
{
    await WithClientServiceAsync(async (_, service, _) =>
    {
        var client = CreateClient("Alan", "Turing");
        await service.CreateAsync(client);
        await service.DeleteAsync(client.Id);
        Assert(await service.GetByIdAsync(client.Id) is null, "Le client supprimé est encore lisible.");
    });
}

static async Task TestClientSearchAsync()
{
    await WithClientServiceAsync(async (_, service, _) =>
    {
        await service.CreateAsync(CreateClient("Marie", "Curie", "Paris"));
        await service.CreateAsync(CreateClient("Niels", "Bohr", "Copenhagen"));
        var results = await service.SearchAsync("paris");
        Assert(results.Count == 1 && results[0].LastName == "Curie", "La recherche ne respecte pas le filtre attendu.");
    });
}

static async Task TestClientConcurrentOperationsAsync()
{
    await WithClientServiceAsync(async (_, service, _) =>
    {
        var clients = Enumerable.Range(1, 10).Select(index => CreateClient($"First{index}", $"Last{index}")).ToList();
        await Task.WhenAll(clients.Select(client => service.CreateAsync(client)));
        var stored = await service.GetAllAsync();
        Assert(stored.Count == clients.Count, "Les opérations concurrentes ont perdu des clients.");
    });
}

static async Task TestClientReuseAfterExceptionAsync()
{
    await WithClientServiceAsync(async (_, service, _) =>
    {
        var existing = CreateClient("Existing", "Client");
        await service.CreateAsync(existing);
        try
        {
            await service.CreateAsync(existing);
        }
        catch (Exception)
        {
        }

        var client = CreateClient("Katherine", "Johnson");
        await service.CreateAsync(client);
        Assert(await service.GetByIdAsync(client.Id) is not null, "Le service n'est pas réutilisable après exception.");
    });
}

static Task TestClientNoIndependentConnectionAsync()
{
    var fields = typeof(SqliteClientService).GetFields(
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Public);
    Assert(fields.All(field => field.FieldType != typeof(SQLite.SQLiteAsyncConnection)), "SqliteClientService contient encore une connexion indépendante.");
    var constructor = typeof(SqliteClientService).GetConstructor([typeof(ISqliteDatabase), typeof(IClientBackupService)]);
    Assert(constructor is not null, "Le constructeur injectant ISqliteDatabase est absent.");
    return Task.CompletedTask;
}

static async Task TestClientBackupRegenerationAsync()
{
    await WithClientServiceAsync(async (_, service, backup) =>
    {
        var client = CreateClient("Dorothy", "Vaughan");
        await service.CreateAsync(client);
        Assert(backup.RegenerationCount == 1 && backup.LastClients.Count == 1, "La sauvegarde JSON n'a pas été régénérée après création.");
        client.City = "Hampton";
        await service.UpdateAsync(client);
        await service.DeleteAsync(client.Id);
        Assert(backup.RegenerationCount == 3 && backup.LastClients.Count == 0, "Les sauvegardes ne suivent pas les modifications CRUD.");
    });
}

static async Task TestClientHistoricalDataAsync()
{
    var root = CreateTemporaryDirectory();
    var path = Path.Combine(root, "historical.db3");
    var coordinator = new SQLiteDatabaseCoordinator(path);
    var backup = new ClientBackupStub();
    try
    {
        await coordinator.ExecuteWriteAsync(connection => connection.CreateTableAsync<Client>());
        var historical = CreateClient("Hedy", "Lamarr");
        await coordinator.ExecuteWriteAsync(connection => connection.InsertAsync(historical));
        var service = new SqliteClientService(coordinator, backup);
        var result = await service.GetByIdAsync(historical.Id);
        Assert(result?.LastName == "Lamarr", "La donnée historique n'est pas compatible avec le service migré.");
    }
    finally
    {
        try { await coordinator.DisposeAsync(); } catch { }
        DeleteTemporaryDirectory(root);
    }
}

static async Task WithClientServiceAsync(Func<string, SqliteClientService, ClientBackupStub, Task> test)
{
    var root = CreateTemporaryDirectory();
    var path = Path.Combine(root, "clients.db3");
    var coordinator = new SQLiteDatabaseCoordinator(path);
    var backup = new ClientBackupStub();
    var service = new SqliteClientService(coordinator, backup);
    try
    {
        await test(path, service, backup);
    }
    finally
    {
        try { await coordinator.DisposeAsync(); } catch { }
        DeleteTemporaryDirectory(root);
    }
}

static Client CreateClient(string firstName, string lastName, string city = "")
{
    var now = DateTime.UtcNow;
    return new Client
    {
        Id = Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        City = city,
        CreatedAt = now,
        UpdatedAt = now
    };
}

static async Task WithCoordinatorAsync(Func<SQLiteDatabaseCoordinator, Task> test)
{
    var root = CreateTemporaryDirectory();
    var coordinator = new SQLiteDatabaseCoordinator(Path.Combine(root, "database.db3"));
    try
    {
        await test(coordinator);
    }
    finally
    {
        try { await coordinator.DisposeAsync(); } catch { }
        DeleteTemporaryDirectory(root);
    }
}

static async Task<Exception> CaptureExceptionAsync(Func<Task> operation)
{
    try
    {
        await operation();
    }
    catch (Exception exception)
    {
        return exception;
    }

    throw new InvalidOperationException("Une exception était attendue.");
}

static async Task AssertThrowsAsync<TException>(Func<Task> operation)
    where TException : Exception
{
    var exception = await CaptureExceptionAsync(operation);
    Assert(exception is TException, $"Exception attendue : {typeof(TException).Name}, obtenue : {exception.GetType().Name}.");
}

static TaskCompletionSource NewSignal()
{
    return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
}

static string CreateTemporaryDirectory()
{
    var path = Path.Combine(Path.GetTempPath(), "um-coordinator-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static void DeleteTemporaryDirectory(string path)
{
    if (Directory.Exists(path))
    {
        Directory.Delete(path, recursive: true);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
