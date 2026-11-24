using SQLitePCL;
using UltimateManager.Models;
using UltimateManager.Models.DynamicForms;
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
    ("28_CLIENT_HISTORICAL_DATA", TestClientHistoricalDataAsync),
    ("29_DYNAMIC_FIELD_BATCH_ROLLBACK", TestDynamicFieldBatchRollbackAsync),
    ("30_BLOCK_ADDED_AFTER_RECORD_CREATION_EDITABLE", TestBlockAddedAfterRecordEditableAsync),
    ("31_TEMPLATE_BLOCK_EDITABLE_AND_RELOAD", TestTemplateBlockEditableAndReloadAsync),
    ("32_EMPTY_AND_DUPLICATE_LABELS", TestEmptyAndDuplicateLabelsAsync),
    ("33_LABEL_RENAME_KEEPS_IDENTITY_AND_VALUES", TestLabelRenameKeepsValuesAsync),
    ("34_CONCURRENT_RECORD_LOADS_NO_DUPLICATE_INSTANCES", TestConcurrentRecordLoadsNoDuplicatesAsync),
    ("35_EXISTING_INSTANCES_PRESERVED_AND_BLOCK_RENAME", TestExistingInstancesPreservedAndBlockRenameAsync),
    ("36_CONCURRENT_GET_OR_CREATE_SINGLE_RECORD", TestConcurrentGetOrCreateSingleRecordAsync),
    ("37_LEGACY_DUPLICATE_AND_ORPHAN_INSTANCES_TOLERATED", TestLegacyDuplicateAndOrphanInstancesAsync),
    ("38_SAVE_AND_RELOAD_AFTER_RESTART", TestSaveAndReloadAfterRestartAsync),
    ("39_TEMPLATE_SWITCH_PRESERVES_EXISTING_DATA", TestTemplateSwitchPreservesExistingDataAsync)
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

static async Task TestDynamicFieldBatchRollbackAsync()
{
    var root = CreateTemporaryDirectory();
    var previousAppDataDirectory = FileSystem.AppDataDirectory;
    SqliteDynamicFormService? service = null;
    try
    {
        FileSystem.AppDataDirectory = root;
        service = new SqliteDynamicFormService(new WorkspaceContextStub(Guid.NewGuid()));
        var template = await service.CreateTemplateAsync("Test client");
        var block = await service.AddBlockAsync(template.Template.Id, "Coordonnées");
        var field = await service.AddFieldAsync(block.Id, "Téléphone", FieldType.ShortText);
        var record = await service.CreateRecordAsync(template.Template.Id);
        await service.SaveFieldValueAsync(record.Record.Id, field.Id, "\"avant\"");

        await AssertThrowsAsync<InvalidOperationException>(() => service.SaveFieldValuesAsync(
            record.Record.Id,
            new Dictionary<Guid, string>
            {
                [field.Id] = "\"après\"",
                [Guid.NewGuid()] = "\"champ absent\""
            }));

        var reloaded = await service.GetRecordAsync(record.Record.Id)
            ?? throw new InvalidOperationException("Le record de test n’a pas pu être rechargé.");
        var savedValue = reloaded.Blocks.SelectMany(item => item.Values)
            .Single(item => item.FieldDefinitionId == field.Id);
        Assert(savedValue.ValueJson == "\"avant\"", "Le rollback n'a pas conservé la valeur antérieure.");
    }
    finally
    {
        if (service is not null)
        {
            await service.DisposeAsync();
        }

        FileSystem.AppDataDirectory = previousAppDataDirectory;
        DeleteTemporaryDirectory(root);
    }
}

static async Task WithDynamicServiceAsync(Func<SqliteDynamicFormService, Task> test)
{
    var root = CreateTemporaryDirectory();
    var previous = FileSystem.AppDataDirectory;
    SqliteDynamicFormService? service = null;
    try
    {
        FileSystem.AppDataDirectory = root;
        service = new SqliteDynamicFormService(new WorkspaceContextStub(Guid.NewGuid()));
        await test(service);
    }
    finally
    {
        if (service is not null)
        {
            await service.DisposeAsync();
        }

        FileSystem.AppDataDirectory = previous;
        DeleteTemporaryDirectory(root);
    }
}

static string ValueOf(EntityRecordModel record, Guid fieldId) =>
    record.Blocks.SelectMany(item => item.Values).Single(item => item.FieldDefinitionId == fieldId).ValueJson;

static Task TestBlockAddedAfterRecordEditableAsync() => WithDynamicServiceAsync(async service =>
{
    var template = await service.CreateTemplateAsync("Fiche");
    var oldBlock = await service.AddBlockAsync(template.Template.Id, "Ancien");
    var oldField = await service.AddFieldAsync(oldBlock.Id, "A", FieldType.ShortText);
    var externalId = Guid.NewGuid();
    var record = await service.GetOrCreateRecordAsync(externalId, template.Template.Id);
    var newBlock = await service.AddBlockAsync(template.Template.Id, "Recent");
    var newField = await service.AddFieldAsync(newBlock.Id, "B", FieldType.LongText);
    var otherField = await service.AddFieldAsync(newBlock.Id, "C", FieldType.Checkbox);
    await service.SaveFieldValuesAsync(record.Record.Id, new Dictionary<Guid, string>
    {
        [oldField.Id] = "\"ancien\"",
        [newField.Id] = "\"recent\""
    });

    var reloaded = await service.GetOrCreateRecordAsync(externalId, template.Template.Id);
    Assert(reloaded.Blocks.Count == 2, "Le bloc ajouté après la création de la fiche n'a pas d'instance.");
    Assert(ValueOf(reloaded, oldField.Id) == "\"ancien\"", "Valeur de l'ancien bloc perdue.");
    Assert(ValueOf(reloaded, newField.Id) == "\"recent\"", "Valeur du bloc récent non relue.");
    Assert(reloaded.Blocks.SelectMany(item => item.Values).All(item => item.FieldDefinitionId != otherField.Id), "Champ sans valeur : aucune valeur attendue.");
    var again = await service.GetRecordAsync(record.Record.Id);
    Assert(again!.Blocks.Count == 2, "La réconciliation doit être idempotente.");
});

static Task TestTemplateBlockEditableAndReloadAsync() => WithDynamicServiceAsync(async service =>
{
    var source = await service.CreateTemplateAsync("Modèle");
    var block = await service.AddBlockAsync(source.Template.Id, "Bloc modèle");
    await service.AddFieldAsync(block.Id, "Un", FieldType.ShortText);
    await service.AddFieldAsync(block.Id, "Deux", FieldType.Phone);
    var client = await service.CloneTemplateAsync(source.Template.Id, "Fiche cliente");
    var externalId = Guid.NewGuid();
    var record = await service.GetOrCreateRecordAsync(externalId, client.Template.Id);
    var fields = client.Blocks.Single().Fields;
    await service.SaveFieldValuesAsync(record.Record.Id, fields.ToDictionary(field => field.Id, field => $"\"{field.Label}\""));
    var reloaded = await service.GetOrCreateRecordAsync(externalId, client.Template.Id);
    foreach (var field in fields)
    {
        Assert(ValueOf(reloaded, field.Id) == $"\"{field.Label}\"", $"Valeur du champ {field.Label} non rechargée.");
    }
});

static Task TestEmptyAndDuplicateLabelsAsync() => WithDynamicServiceAsync(async service =>
{
    var template = await service.CreateTemplateAsync("Fiche");
    var block = await service.AddBlockAsync(template.Template.Id, "   ");
    var empty = await service.AddFieldAsync(block.Id, "", FieldType.ShortText);
    var first = await service.AddFieldAsync(block.Id, "Doublon", FieldType.ShortText);
    var second = await service.AddFieldAsync(block.Id, "Doublon", FieldType.ShortText);
    var loaded = await service.GetTemplateAsync(template.Template.Id);
    var fields = loaded!.Blocks.Single().Fields;
    Assert(fields.Count == 3, "Un champ à label vide ou dupliqué a été perdu.");
    Assert(fields.Select(field => field.Id).Distinct().Count() == 3, "Collision d'identifiants.");
    Assert(first.Id != second.Id && empty.Id != first.Id, "Les identifiants doivent être indépendants des labels.");
    Assert(fields.Single(field => field.Id == empty.Id).Label.Length == 0, "Le label vide doit être conservé tel quel.");
    var record = await service.CreateRecordAsync(template.Template.Id);
    await service.SaveFieldValuesAsync(record.Record.Id, new Dictionary<Guid, string>
    {
        [first.Id] = "\"1\"",
        [second.Id] = "\"2\""
    });
    var reloaded = await service.GetRecordAsync(record.Record.Id);
    Assert(ValueOf(reloaded!, first.Id) == "\"1\"" && ValueOf(reloaded!, second.Id) == "\"2\"", "Valeurs mélangées entre labels identiques.");
});

static Task TestLabelRenameKeepsValuesAsync() => WithDynamicServiceAsync(async service =>
{
    var template = await service.CreateTemplateAsync("Fiche");
    var block = await service.AddBlockAsync(template.Template.Id, "Bloc");
    var target = await service.AddFieldAsync(block.Id, "Ancien label", FieldType.ShortText);
    var neighbour = await service.AddFieldAsync(block.Id, "Voisin", FieldType.ShortText);
    var externalId = Guid.NewGuid();
    var record = await service.GetOrCreateRecordAsync(externalId, template.Template.Id);
    await service.SaveFieldValuesAsync(record.Record.Id, new Dictionary<Guid, string>
    {
        [target.Id] = "\"valeur cible\"",
        [neighbour.Id] = "\"valeur voisin\""
    });

    var definition = (await service.GetTemplateAsync(template.Template.Id))!.Blocks.Single().Fields.Single(field => field.Id == target.Id);
    definition.Label = "  Nouveau label ";
    await service.UpdateFieldAsync(definition);

    var fields = (await service.GetTemplateAsync(template.Template.Id))!.Blocks.Single().Fields;
    var renamed = fields.Single(field => field.Id == target.Id);
    Assert(renamed.Label == "Nouveau label", "Le label n'a pas été modifié.");
    Assert(renamed.FieldType == FieldType.ShortText && renamed.DisplayOrder == target.DisplayOrder, "Type/ordre modifiés par le renommage.");
    Assert(fields.Single(field => field.Id == neighbour.Id).Label == "Voisin", "Un autre champ a été affecté.");
    var reloaded = await service.GetOrCreateRecordAsync(externalId, template.Template.Id);
    Assert(ValueOf(reloaded, target.Id) == "\"valeur cible\"", "Valeur du champ renommé perdue.");
    Assert(ValueOf(reloaded, neighbour.Id) == "\"valeur voisin\"", "Valeur du voisin affectée.");
    Assert(reloaded.Record.Id == record.Record.Id, "La fiche a été recréée.");
});

static Task TestTemplateSwitchPreservesExistingDataAsync() => WithDynamicServiceAsync(async service =>
{
    var originalTemplate = await service.CreateTemplateAsync("Modèle d'origine");
    var originalBlock = await service.AddBlockAsync(originalTemplate.Template.Id, "Bloc d'origine");
    var originalField = await service.AddFieldAsync(originalBlock.Id, "Information", FieldType.ShortText);
    var replacementTemplate = await service.CreateTemplateAsync("Nouveau modèle");
    var externalId = Guid.NewGuid();
    var originalRecord = await service.GetOrCreateRecordAsync(externalId, originalTemplate.Template.Id);
    await service.SaveFieldValueAsync(originalRecord.Record.Id, originalField.Id, "\"à conserver\"");

    var rejected = false;
    try
    {
        await service.GetOrCreateRecordAsync(externalId, replacementTemplate.Template.Id);
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains("autre modèle", StringComparison.OrdinalIgnoreCase))
    {
        rejected = true;
    }

    Assert(rejected, "Le changement de modèle doit être refusé sans migration de données.");
    var preserved = await service.GetOrCreateRecordAsync(externalId, originalTemplate.Template.Id);
    Assert(preserved.Record.Id == originalRecord.Record.Id, "L'identifiant de la fiche existante doit rester inchangé.");
    Assert(ValueOf(preserved, originalField.Id) == "\"à conserver\"", "La valeur existante doit rester inchangée après le refus.");
});

static Task TestConcurrentRecordLoadsNoDuplicatesAsync() => WithDynamicServiceAsync(async service =>
{
    var template = await service.CreateTemplateAsync("Fiche");
    var first = await service.AddBlockAsync(template.Template.Id, "Premier");
    await service.AddFieldAsync(first.Id, "A", FieldType.ShortText);
    var externalId = Guid.NewGuid();
    var record = await service.GetOrCreateRecordAsync(externalId, template.Template.Id);
    for (var index = 0; index < 4; index++)
    {
        await service.AddBlockAsync(template.Template.Id, $"Ajout {index}");
    }

    var loads = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => service.GetRecordAsync(record.Record.Id)));
    Assert(loads.All(item => item is not null), "Un chargement concurrent a échoué.");
    var final = await service.GetRecordAsync(record.Record.Id);
    var definitionIds = final!.Blocks.Select(block => block.Block.BlockDefinitionId).ToList();
    Assert(definitionIds.Count == 5, $"5 instances attendues, obtenu {definitionIds.Count}.");
    Assert(definitionIds.Distinct().Count() == 5, "Doublons d'instances après chargements concurrents.");
    var viaGetOrCreate = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.GetOrCreateRecordAsync(externalId, template.Template.Id)));
    Assert(viaGetOrCreate.All(item => item.Record.Id == record.Record.Id && item.Blocks.Count == 5), "GetOrCreateRecordAsync concurrent a recréé ou dupliqué.");
});

static Task TestExistingInstancesPreservedAndBlockRenameAsync() => WithDynamicServiceAsync(async service =>
{
    var template = await service.CreateTemplateAsync("Fiche");
    var block = await service.AddBlockAsync(template.Template.Id, "Bloc");
    var field = await service.AddFieldAsync(block.Id, "Champ", FieldType.ShortText);
    var externalId = Guid.NewGuid();
    var record = await service.GetOrCreateRecordAsync(externalId, template.Template.Id);
    await service.SaveFieldValueAsync(record.Record.Id, field.Id, "\"valeur\"");
    var instanceBefore = record.Blocks.Single().Block.Id;

    var definition = (await service.GetTemplateAsync(template.Template.Id))!.Blocks.Single().Block;
    definition.Name = "Bloc renommé";
    await service.UpdateBlockAsync(definition);
    await service.AddBlockAsync(template.Template.Id, "Nouveau");

    var reloaded = await service.GetOrCreateRecordAsync(externalId, template.Template.Id);
    Assert(reloaded.Blocks.Count == 2, "Instance manquante non créée.");
    Assert(reloaded.Blocks.Any(item => item.Block.Id == instanceBefore), "L'instance existante a été remplacée.");
    Assert(ValueOf(reloaded, field.Id) == "\"valeur\"", "Valeur perdue après renommage du bloc.");
    var template2 = await service.GetTemplateAsync(template.Template.Id);
    Assert(template2!.Blocks.Any(item => item.Block.Id == block.Id && item.Block.Name == "Bloc renommé"), "Nom de bloc non modifié ou identité changée.");
});

static Task TestConcurrentGetOrCreateSingleRecordAsync() => WithDynamicServiceAsync(async service =>
{
    var template = await service.CreateTemplateAsync("Fiche");
    await service.AddBlockAsync(template.Template.Id, "A");
    await service.AddBlockAsync(template.Template.Id, "B");
    var externalId = Guid.NewGuid();
    var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => service.GetOrCreateRecordAsync(externalId, template.Template.Id)));
    Assert(results.Select(item => item.Record.Id).Distinct().Count() == 1, "Plusieurs fiches créées pour la même entité.");
    Assert(results.All(item => item.Blocks.Count == 2), "Nombre de blocs incorrect après création concurrente.");
    using var raw = new SQLite.SQLiteConnection(Path.Combine(FileSystem.AppDataDirectory, "ultimate-manager.db3"));
    Assert(raw.Table<EntityRecord>().Count(item => item.ExternalEntityId == externalId) == 1, "Doublon d'EntityRecord en base.");
    Assert(raw.Table<BlockInstance>().Count() == 2, "Doublon de BlockInstance en base.");
});

static Task TestLegacyDuplicateAndOrphanInstancesAsync() => WithDynamicServiceAsync(async service =>
{
    var template = await service.CreateTemplateAsync("Fiche");
    var block = await service.AddBlockAsync(template.Template.Id, "Bloc");
    var field = await service.AddFieldAsync(block.Id, "Champ", FieldType.ShortText);
    var record = await service.CreateRecordAsync(template.Template.Id);
    await service.SaveFieldValueAsync(record.Record.Id, field.Id, "\"v\"");
    using (var raw = new SQLite.SQLiteConnection(Path.Combine(FileSystem.AppDataDirectory, "ultimate-manager.db3")))
    {
        raw.Insert(new BlockInstance { Id = Guid.NewGuid(), EntityRecordId = record.Record.Id, BlockDefinitionId = block.Id, Name = "Doublon", DisplayOrder = 0 });
        raw.Insert(new BlockInstance { Id = Guid.NewGuid(), EntityRecordId = record.Record.Id, BlockDefinitionId = Guid.NewGuid(), Name = "Orphelin", DisplayOrder = 5 });
    }

    var loaded = await service.GetRecordAsync(record.Record.Id);
    Assert(loaded!.Blocks.Count == 1, $"Une seule instance attendue par définition, obtenu {loaded.Blocks.Count}.");
    Assert(ValueOf(loaded, field.Id) == "\"v\"", "Valeur perdue avec doublon historique.");
    using var check = new SQLite.SQLiteConnection(Path.Combine(FileSystem.AppDataDirectory, "ultimate-manager.db3"));
    Assert(check.Table<BlockInstance>().Count() == 3, "La lecture ne doit rien supprimer.");
    Assert(check.Table<FieldValue>().Count() == 1, "Valeur supprimée.");
});

static async Task TestSaveAndReloadAfterRestartAsync()
{
    var root = CreateTemporaryDirectory();
    var previous = FileSystem.AppDataDirectory;
    var workspace = new WorkspaceContextStub(Guid.NewGuid());
    try
    {
        FileSystem.AppDataDirectory = root;
        var externalId = Guid.NewGuid();
        Guid templateId, fieldId, recordId;
        var first = new SqliteDynamicFormService(workspace);
        try
        {
            var template = await first.CreateTemplateAsync("Fiche");
            templateId = template.Template.Id;
            var block = await first.AddBlockAsync(templateId, "Ancien");
            fieldId = (await first.AddFieldAsync(block.Id, "Champ", FieldType.ShortText)).Id;
            var record = await first.GetOrCreateRecordAsync(externalId, templateId);
            recordId = record.Record.Id;
            await first.SaveFieldValueAsync(recordId, fieldId, "\"persisté\"");
            await first.AddBlockAsync(templateId, "Ajouté après");
        }
        finally
        {
            await first.DisposeAsync();
        }

        var second = new SqliteDynamicFormService(workspace);
        try
        {
            var reloaded = await second.GetOrCreateRecordAsync(externalId, templateId);
            Assert(reloaded.Record.Id == recordId, "Fiche recréée après redémarrage.");
            Assert(reloaded.Blocks.Count == 2, "Bloc ajouté après la fiche absent après redémarrage.");
            Assert(ValueOf(reloaded, fieldId) == "\"persisté\"", "Valeur non relû après redémarrage.");
        }
        finally
        {
            await second.DisposeAsync();
        }
    }
    finally
    {
        FileSystem.AppDataDirectory = previous;
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

sealed class WorkspaceContextStub(Guid workspaceId) : IWorkspaceContext
{
    public Guid WorkspaceId { get; } = workspaceId;

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
