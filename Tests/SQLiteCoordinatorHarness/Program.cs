using SQLitePCL;
using System.Text.Json;
using System.IO.Compression;
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
    ("39_TEMPLATE_SWITCH_PRESERVES_EXISTING_DATA", TestTemplateSwitchPreservesExistingDataAsync),
    ("40_USED_TEMPLATES_REMAIN_AVAILABLE_IN_WORKSPACE", TestUsedTemplatesRemainAvailableAsync),
    ("41_COMPLETE_BACKUP_RESTORE_FAILS_CLOSED", TestCompleteBackupRestoreFailsClosedAsync),
    ("42_CLIENT_DRAFT_CREATION_ATOMIC_AND_REMAPS_VALUES", TestClientDraftCreationAtomicAndRemapsValuesAsync),
    ("43_DRAFT_TEMPLATE_SWITCH_AND_CANCEL_NO_WRITES", TestDraftTemplateSwitchAndCancelNoWritesAsync),
    ("44_WORKSPACE_ISOLATION_AND_CLIENT_CASCADE", TestWorkspaceIsolationAndClientCascadeAsync),
    ("45_LEGACY_CLIENT_SCHEMA_MIGRATION_IDEMPOTENT", TestLegacyClientSchemaMigrationIdempotentAsync),
    ("46_COMPLETE_BACKUP_WORKSPACE_SCOPE_AND_TEMP_CLEANUP", TestCompleteBackupWorkspaceScopeAndTempCleanupAsync),
    ("47_CLIENT_EDIT_SAVE_CREATES_DRAFT_ONCE_AND_LOADS_PERSISTED_TEMPLATE", TestClientEditSaveCreatesDraftOnceAsync),
    ("48_DYNAMIC_BLOCK_FIELD_DELETE_CASCADE", TestDynamicBlockFieldDeleteCascadeAsync),
    ("49_COMPLETE_BACKUP_FAILURE_CLEANS_TEMP", TestCompleteBackupFailureCleansTempAsync)
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
    finally
    {
        await TestCoordinatorRegistry.DisposeAllAsync();
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
    var constructor = typeof(SqliteClientService).GetConstructor([typeof(ISqliteDatabase), typeof(IWorkspaceContext), typeof(IClientBackupService)]);
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
    var workspace = new WorkspaceContextStub(Guid.NewGuid());
    var backup = new ClientBackupStub();
    try
    {
        await coordinator.ExecuteWriteAsync(connection => connection.CreateTableAsync<Client>());
        var historical = CreateClient("Hedy", "Lamarr");
        historical.WorkspaceId = Guid.Empty;
        await coordinator.ExecuteWriteAsync(connection => connection.InsertAsync(historical));
        var service = new SqliteClientService(coordinator, workspace, backup);
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
    SQLiteDatabaseCoordinator? coordinator = null;
    try
    {
        FileSystem.AppDataDirectory = root;
        coordinator = CreateTestCoordinator(root);
        service = new SqliteDynamicFormService(coordinator, new WorkspaceContextStub(Guid.NewGuid()));
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

        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
            TestCoordinatorRegistry.Remove(coordinator);
        }

        FileSystem.AppDataDirectory = previousAppDataDirectory;
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestClientDraftCreationAtomicAndRemapsValuesAsync()
{
    var root = CreateTemporaryDirectory();
    var previous = FileSystem.AppDataDirectory;
    SqliteDynamicFormService? service = null;
    SQLiteDatabaseCoordinator? coordinator = null;
    try
    {
        FileSystem.AppDataDirectory = root;
        coordinator = CreateTestCoordinator(root);
        var workspace = new WorkspaceContextStub(Guid.NewGuid());
        service = new SqliteDynamicFormService(coordinator, workspace);
        var client = CreateClient("Brouillon", "Test");
        var blockId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var draft = new TemplateDefinition
        {
            Template = new EntityTemplate { Id = Guid.NewGuid(), Name = "Modèle choisi", EntityType = "Client" },
            Blocks =
            [
                new BlockDefinitionModel
                {
                    Block = new BlockDefinition { Id = blockId, Name = "Informations", DisplayOrder = 0 },
                    Fields = [new FieldDefinition { Id = fieldId, Label = "Courriel", FieldType = FieldType.Email, DisplayOrder = 0 }]
                }
            ]
        };
        var templatesBefore = await service.GetTemplatesAsync();

        var created = await service.CreateClientWithDraftAsync(
            client,
            draft,
            new Dictionary<Guid, string> { [fieldId] = "\"qa@example.test\"" });

        Assert(created.Record.ExternalEntityId == client.Id, "Le record créé ne référence pas le client attendu.");
        Assert(created.Record.WorkspaceId == workspace.WorkspaceId, "Le record n'est pas isolé dans le Workspace courant.");
        Assert(created.Record.TemplateId != draft.Template.Id, "Le template temporaire aurait dû être cloné avec un nouvel identifiant.");
        var persistedField = created.Blocks.SelectMany(item => item.Values).Single();
        Assert(persistedField.FieldDefinitionId != fieldId, "L'identifiant temporaire du champ n'a pas été remappé.");
        Assert(persistedField.ValueJson == "\"qa@example.test\"", "La valeur du brouillon n'a pas été conservée.");
        var templatesAfter = await service.GetTemplatesAsync();
        Assert(templatesAfter.Count == templatesBefore.Count + 1, "La création doit ajouter exactement un template privé.");

        await AssertThrowsAsync<InvalidOperationException>(() => service.CreateClientWithDraftAsync(
            client,
            draft,
            new Dictionary<Guid, string> { [fieldId] = "\"ne pas enregistrer\"" }));
        var templatesAfterDuplicate = await service.GetTemplatesAsync();
        Assert(templatesAfterDuplicate.Count == templatesAfter.Count, "L'échec d'une création dupliquée a laissé un template orphelin.");
        var existing = await service.GetRecordForExternalEntityAsync(client.Id)
            ?? throw new InvalidOperationException("Le record initial a disparu après le rejet du doublon.");
        Assert(existing.Record.Id == created.Record.Id, "Le rejet du doublon a modifié le record existant.");

        var rollbackClient = CreateClient("Rollback", "Test");
        var invalidDraft = new TemplateDefinition
        {
            Template = new EntityTemplate { Id = Guid.NewGuid(), Name = "Brouillon invalide", EntityType = "Client" },
            Blocks = [new BlockDefinitionModel { Block = new BlockDefinition { Id = Guid.NewGuid(), Name = null!, DisplayOrder = 0 } }]
        };
        _ = await CaptureExceptionAsync(() => service.CreateClientWithDraftAsync(rollbackClient, invalidDraft, new Dictionary<Guid, string>()));
        var rollbackArtifacts = await coordinator.ReadAsync(async connection =>
            new
            {
                Client = await connection.Table<Client>().Where(item => item.Id == rollbackClient.Id).FirstOrDefaultAsync(),
                TemplateCount = await connection.Table<EntityTemplate>().CountAsync(),
                Record = await connection.Table<EntityRecord>().Where(record => record.ExternalEntityId == rollbackClient.Id).FirstOrDefaultAsync()
            });
        Assert(rollbackArtifacts.Client is null, "Le rollback a laissé le client inséré.");
        Assert(rollbackArtifacts.TemplateCount == templatesAfter.Count, "Le rollback a laissé le template inséré.");
        Assert(rollbackArtifacts.Record is null, "Le rollback a laissé le record inséré.");
    }
    finally
    {
        if (service is not null)
        {
            await service.DisposeAsync();
        }

        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
            TestCoordinatorRegistry.Remove(coordinator);
        }

        FileSystem.AppDataDirectory = previous;
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestDraftTemplateSwitchAndCancelNoWritesAsync()
{
    var service = new DynamicFormServiceStub();
    var viewModel = new UltimateManager.ViewModels.ClientDynamicRecordViewModel(
        service,
        new DynamicFieldValueSerializer(),
        new MapProviderStub(),
        new AutomaticBackupStub());
    var templateA = CreateDraftTemplate(
        "A",
        ("Email", FieldType.Email),
        ("Nom", FieldType.ShortText));
    var templateB = CreateDraftTemplate(
        "B",
        (" eMaIl ", FieldType.Email),
        ("Nom", FieldType.ShortText));

    await viewModel.LoadDraftAsync(templateA);
    var fieldsA = viewModel.Blocks.SelectMany(block => block.Fields).ToList();
    fieldsA.Single(field => field.Label == "Email").Value = "qa@example.test";
    fieldsA.Single(field => field.Label == "Nom").Value = "Client A";

    await viewModel.LoadDraftAsync(templateB);
    var fieldsB = viewModel.Blocks.SelectMany(block => block.Fields).ToList();
    Assert(fieldsB.Single(field => field.Label == " eMaIl ").Value == "qa@example.test", "La valeur compatible ne suit pas le champ renommé par casse/espaces.");
    Assert(fieldsB.Single(field => field.Label == "Nom").Value == "Client A", "La valeur compatible n'a pas été conservée pour le second champ.");

    var ambiguousTemplate = CreateDraftTemplate("C", ("Email", FieldType.Email), (" email ", FieldType.Email));
    await viewModel.LoadDraftAsync(ambiguousTemplate);
    Assert(viewModel.Blocks.SelectMany(block => block.Fields).All(field => string.IsNullOrEmpty(field.Value)),
        "Les champs ambigus du template cible ne doivent pas recevoir une valeur transférée.");
    Assert(service.PersistenceWrites == 0 && service.PersistedTemplates.Count == 0 && service.PersistedRecords.Count == 0,
        "La sélection/changement de template ne doit pas écrire en base.");

    viewModel.ResetDraft();
    Assert(viewModel.Blocks.Count == 0, "L'annulation doit vider le brouillon en mémoire.");
    Assert(service.PersistenceWrites == 0 && service.PersistedTemplates.Count == 0 && service.PersistedRecords.Count == 0,
        "L'annulation d'un brouillon doit laisser les données persistantes intactes.");
}

static async Task TestClientEditSaveCreatesDraftOnceAsync()
{
    var formService = new DynamicFormServiceStub();
    var actualTemplateId = Guid.NewGuid();
    formService.ActualTemplateId = actualTemplateId;
    var recordViewModel = new UltimateManager.ViewModels.ClientDynamicRecordViewModel(
        formService,
        new DynamicFieldValueSerializer(),
        new MapProviderStub(),
        new AutomaticBackupStub());
    var viewModel = new UltimateManager.ViewModels.ClientEditViewModel(
        new ClientServiceStub(),
        new MapProviderStub(),
        formService,
        recordViewModel);
    var template = CreateDraftTemplate("Default", ("Email", FieldType.Email));

    await recordViewModel.LoadDraftAsync(template);
    viewModel.Load(null);
    // Load creates the new client state; the form draft is attached as it is in the page workflow.
    viewModel.FirstName = "Test";
    viewModel.LastName = "Client";

    var saved = await viewModel.SaveAsync();

    Assert(saved, "SaveAsync a refusé la création d'un client valide.");
    Assert(formService.PersistenceWrites == 1, "La création atomique doit être appelée exactement une fois.");
    Assert(formService.LastLoadedTemplateId == actualTemplateId,
        $"Le ViewModel a rechargé {formService.LastLoadedTemplateId} au lieu du template persisté {actualTemplateId}.");
    Assert(viewModel.IsEditing, "Le ViewModel doit passer en mode édition après la création.");
}

static Task TestDynamicBlockFieldDeleteCascadeAsync() => WithDynamicServiceAsync(async service =>
{
    var template = await service.CreateTemplateAsync("Suppression");
    var block = await service.AddBlockAsync(template.Template.Id, "Bloc principal");
    var fieldToDelete = await service.AddFieldAsync(block.Id, "À supprimer", FieldType.ShortText);
    var retainedField = await service.AddFieldAsync(block.Id, "À garder", FieldType.ShortText);
    var record = await service.CreateRecordAsync(template.Template.Id);
    await service.SaveFieldValuesAsync(record.Record.Id, new Dictionary<Guid, string>
    {
        [fieldToDelete.Id] = "\"supprimé\"",
        [retainedField.Id] = "\"gardé\""
    });

    await service.DeleteFieldAsync(fieldToDelete.Id);
    var afterFieldDelete = await service.GetRecordAsync(record.Record.Id)
        ?? throw new InvalidOperationException("Record absent après suppression du champ.");
    Assert(afterFieldDelete.Blocks.SelectMany(item => item.Values).All(value => value.FieldDefinitionId != fieldToDelete.Id),
        "La suppression du champ a laissé une valeur orpheline.");
    Assert(ValueOf(afterFieldDelete, retainedField.Id) == "\"gardé\"", "La valeur voisine a été supprimée avec le champ.");

    await service.DeleteBlockAsync(block.Id);
    var afterBlockDelete = await service.GetRecordAsync(record.Record.Id)
        ?? throw new InvalidOperationException("Record absent après suppression du bloc.");
    Assert(afterBlockDelete.Blocks.Count == 0, "L'instance du bloc supprimé reste présente.");
    Assert(afterBlockDelete.Blocks.SelectMany(item => item.Values).All(value => value.FieldDefinitionId != retainedField.Id),
        "La suppression du bloc a laissé des valeurs orphelines.");
});

static async Task TestCompleteBackupFailureCleansTempAsync()
{
    var root = CreateTemporaryDirectory();
    var previous = FileSystem.AppDataDirectory;
    CompleteBackupService? service = null;
    SQLiteDatabaseCoordinator? coordinator = null;
    try
    {
        FileSystem.AppDataDirectory = root;
        var databasePath = Path.Combine(root, "ultimate-manager.db3");
        var workspaceId = Guid.NewGuid();
        using (var seed = new SQLite.SQLiteConnection(databasePath))
        {
            seed.CreateTable<Workspace>();
            seed.CreateTable<Client>();
            seed.CreateTable<EntityTemplate>();
            seed.CreateTable<BlockDefinition>();
            seed.CreateTable<FieldDefinition>();
            seed.CreateTable<EntityRecord>();
            seed.CreateTable<BlockInstance>();
            seed.CreateTable<FieldValue>();
            var now = DateTime.UtcNow;
            seed.Insert(new Workspace { Id = workspaceId, Name = "Backup incomplet", CreatedAt = now });
            var client = CreateClient("Sans", "Pièce jointe");
            client.WorkspaceId = workspaceId;
            var template = new EntityTemplate { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Docs", CreatedAt = now, UpdatedAt = now };
            var block = new BlockDefinition { Id = Guid.NewGuid(), TemplateId = template.Id, Name = "Documents" };
            var field = new FieldDefinition { Id = Guid.NewGuid(), BlockDefinitionId = block.Id, Label = "Fichier", FieldType = FieldType.Document };
            var record = new EntityRecord { Id = Guid.NewGuid(), WorkspaceId = workspaceId, TemplateId = template.Id, ExternalEntityId = client.Id, CreatedAt = now, UpdatedAt = now };
            seed.Insert(client);
            seed.Insert(template);
            seed.Insert(block);
            seed.Insert(field);
            seed.Insert(record);
            seed.Insert(new FieldValue
            {
                Id = Guid.NewGuid(), EntityRecordId = record.Id, FieldDefinitionId = field.Id,
                ValueJson = new DynamicFieldValueSerializer().Serialize(new DocumentValue { FileName = "missing.pdf", FilePath = Path.Combine(root, "missing.pdf") }),
                UpdatedAt = now
            });
        }

        coordinator = CreateTestCoordinator(root);
        service = new CompleteBackupService(coordinator, new WorkspaceContextStub(workspaceId), new ClientBackupStub(), new DynamicFieldValueSerializer());
        await AssertThrowsAsync<FileNotFoundException>(() => service.CreateAsync());
        Assert(!File.Exists(service.BackupFilePath + ".tmp"), "Un échec de backup a laissé un ZIP temporaire.");
        Assert(!File.Exists(service.BackupFilePath), "Un backup final ne doit pas être publié après échec.");
    }
    finally
    {
        if (service is not null) await service.DisposeAsync();
        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
            TestCoordinatorRegistry.Remove(coordinator);
        }

        FileSystem.AppDataDirectory = previous;
        DeleteTemporaryDirectory(root);
    }
}

static TemplateDefinition CreateDraftTemplate(string name, params (string Label, FieldType Type)[] fields)
{
    var block = new BlockDefinition { Id = Guid.NewGuid(), Name = "Informations", DisplayOrder = 0 };
    return new TemplateDefinition
    {
        Template = new EntityTemplate { Id = Guid.NewGuid(), Name = name, EntityType = "Client" },
        Blocks =
        [
            new BlockDefinitionModel
            {
                Block = block,
                Fields = fields.Select((field, index) => new FieldDefinition
                {
                    Id = Guid.NewGuid(),
                    BlockDefinitionId = block.Id,
                    Label = field.Label,
                    FieldType = field.Type,
                    DisplayOrder = index
                }).ToList()
            }
        ]
    };
}

static async Task TestWorkspaceIsolationAndClientCascadeAsync()
{
    var root = CreateTemporaryDirectory();
    var previous = FileSystem.AppDataDirectory;
    SQLiteDatabaseCoordinator? coordinator = null;
    SqliteDynamicFormService? dynamicA = null;
    SqliteDynamicFormService? dynamicB = null;
    try
    {
        FileSystem.AppDataDirectory = root;
        coordinator = CreateTestCoordinator(root);
        var workspaceA = new WorkspaceContextStub(Guid.NewGuid());
        var workspaceB = new WorkspaceContextStub(Guid.NewGuid());
        var backupA = new ClientBackupStub();
        var backupB = new ClientBackupStub();
        var clientsA = new SqliteClientService(coordinator, workspaceA, backupA);
        var clientsB = new SqliteClientService(coordinator, workspaceB, backupB);
        dynamicA = new SqliteDynamicFormService(coordinator, workspaceA);
        dynamicB = new SqliteDynamicFormService(coordinator, workspaceB);

        var source = await dynamicA.CreateTemplateAsync("Source partagé");
        var sharedBlock = await dynamicA.AddBlockAsync(source.Template.Id, "Données");
        var sharedField = await dynamicA.AddFieldAsync(sharedBlock.Id, "Valeur", FieldType.ShortText);
        var clientA = CreateClient("Client", "Alpha");
        var clientB = CreateClient("Client", "Beta");
        await clientsA.CreateAsync(clientA);
        await clientsA.CreateAsync(clientB);
        var privateA = await dynamicA.EnsureClientTemplateAsync(clientA.Id, source.Template.Id);
        var privateB = await dynamicA.EnsureClientTemplateAsync(clientB.Id, source.Template.Id);
        var recordA = await dynamicA.GetRecordForExternalEntityAsync(clientA.Id)
            ?? throw new InvalidOperationException("Record A manquant.");
        var recordB = await dynamicA.GetRecordForExternalEntityAsync(clientB.Id)
            ?? throw new InvalidOperationException("Record B manquant.");
        var fieldA = privateA.Blocks.SelectMany(block => block.Fields).Single().Id;
        var fieldB = privateB.Blocks.SelectMany(block => block.Fields).Single().Id;
        await dynamicA.SaveFieldValueAsync(recordA.Record.Id, fieldA, "\"A\"");
        await dynamicA.SaveFieldValueAsync(recordB.Record.Id, fieldB, "\"B\"");

        Assert((await clientsA.GetAllAsync()).Count == 2, "Workspace A ne voit pas ses clients.");
        Assert((await clientsB.GetAllAsync()).Count == 0, "Workspace B voit des clients de Workspace A.");
        Assert(await clientsB.GetByIdAsync(clientA.Id) is null, "GetById expose un client d'un autre Workspace.");
        Assert((await clientsB.SearchAsync("Alpha")).Count == 0, "La recherche expose un client d'un autre Workspace.");
        clientA.City = "Modification interdite";
        await AssertThrowsAsync<InvalidOperationException>(() => clientsB.UpdateAsync(clientA));
        await clientsB.DeleteAsync(clientA.Id);
        Assert(await clientsA.GetByIdAsync(clientA.Id) is not null, "Une suppression inter-Workspace a supprimé le client.");

        await clientsA.DeleteAsync(clientA.Id);
        Assert(await clientsA.GetByIdAsync(clientA.Id) is null, "Le client supprimé reste présent.");
        Assert(await dynamicA.GetRecordAsync(recordA.Record.Id) is null, "Le record du client supprimé reste présent.");
        Assert(await dynamicA.GetRecordAsync(recordB.Record.Id) is not null, "Le record de l'autre client a été supprimé.");
        Assert(await dynamicA.GetTemplateAsync(source.Template.Id) is not null, "Le template source partagé a été supprimé.");
        Assert(await dynamicA.GetTemplateAsync(privateB.Template.Id) is not null, "Le template privé du second client a été supprimé.");
        var retained = await dynamicA.GetRecordAsync(recordB.Record.Id)
            ?? throw new InvalidOperationException("Le record du second client a disparu.");
        Assert(retained.Blocks.SelectMany(block => block.Values).Single(value => value.FieldDefinitionId == fieldB).ValueJson == "\"B\"",
            "La valeur dynamique du second client a été modifiée.");

        var workspaceBClient = CreateClient("Autre", "Espace");
        await clientsB.CreateAsync(workspaceBClient);
        Assert((await clientsB.GetAllAsync()).Select(client => client.Id).SequenceEqual([workspaceBClient.Id]),
            "Workspace B ne voit pas uniquement son propre client.");
        Assert(backupA.LastClients.All(client => client.WorkspaceId == workspaceA.WorkspaceId), "Le backup client A contient un autre Workspace.");
        Assert(backupB.LastClients.All(client => client.WorkspaceId == workspaceB.WorkspaceId), "Le backup client B contient un autre Workspace.");
    }
    finally
    {
        if (dynamicA is not null) await dynamicA.DisposeAsync();
        if (dynamicB is not null) await dynamicB.DisposeAsync();
        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
            TestCoordinatorRegistry.Remove(coordinator);
        }

        FileSystem.AppDataDirectory = previous;
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestLegacyClientSchemaMigrationIdempotentAsync()
{
    var root = CreateTemporaryDirectory();
    var path = Path.Combine(root, "legacy-clients.db3");
    var workspaceId = Guid.NewGuid();
    var legacyClient = CreateClient("Client historique", "Conservé");
    try
    {
        using (var legacyConnection = new SQLite.SQLiteConnection(path))
        {
            legacyConnection.CreateTable<LegacyClient>();
            legacyConnection.Insert(new LegacyClient
            {
                Id = legacyClient.Id,
                FirstName = legacyClient.FirstName,
                LastName = legacyClient.LastName,
                PhoneNumber = legacyClient.PhoneNumber,
                Email = legacyClient.Email,
                Address = legacyClient.Address,
                PostalCode = legacyClient.PostalCode,
                City = legacyClient.City,
                Country = legacyClient.Country,
                Latitude = legacyClient.Latitude,
                Longitude = legacyClient.Longitude,
                Comment = legacyClient.Comment,
                CreatedAt = legacyClient.CreatedAt,
                UpdatedAt = legacyClient.UpdatedAt
            });
        }

        var coordinator = new SQLiteDatabaseCoordinator(path);
        TestCoordinatorRegistry.Add(coordinator);
        var backup = new ClientBackupStub();
        var service = new SqliteClientService(coordinator, new WorkspaceContextStub(workspaceId), backup);
        var migrated = await service.GetByIdAsync(legacyClient.Id);
        Assert(migrated?.LastName == legacyClient.LastName && migrated.WorkspaceId == workspaceId,
            "La migration n'a pas conservé les données historiques ou affecté le Workspace courant.");
        await service.GetAllAsync();
        var columns = await coordinator.ReadAsync(connection => connection.GetTableInfoAsync("Clients"));
        Assert(columns.Any(column => column.Name == nameof(Client.WorkspaceId)), "La colonne WorkspaceId n'a pas été ajoutée.");
        var stored = await coordinator.ReadAsync(connection => connection.FindAsync<Client>(legacyClient.Id));
        Assert(stored?.WorkspaceId == workspaceId, "Le rattachement historique n'a pas persisté.");

        var secondCoordinator = new SQLiteDatabaseCoordinator(path);
        TestCoordinatorRegistry.Add(secondCoordinator);
        var secondService = new SqliteClientService(secondCoordinator, new WorkspaceContextStub(workspaceId), new ClientBackupStub());
        var reloaded = await secondService.GetByIdAsync(legacyClient.Id);
        Assert(reloaded?.WorkspaceId == workspaceId, "La migration répétée n'a pas conservé le Workspace.");
        await secondCoordinator.DisposeAsync();
        TestCoordinatorRegistry.Remove(secondCoordinator);
        await coordinator.DisposeAsync();
        TestCoordinatorRegistry.Remove(coordinator);
    }
    finally
    {
        await TestCoordinatorRegistry.DisposeAllAsync();
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestCompleteBackupWorkspaceScopeAndTempCleanupAsync()
{
    var root = CreateTemporaryDirectory();
    var previous = FileSystem.AppDataDirectory;
    CompleteBackupService? service = null;
    SQLiteDatabaseCoordinator? coordinator = null;
    try
    {
        FileSystem.AppDataDirectory = root;
        var databasePath = Path.Combine(root, "ultimate-manager.db3");
        var activeWorkspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        using (var seed = new SQLite.SQLiteConnection(databasePath))
        {
            foreach (var table in new Action[]
                 {
                     () => seed.CreateTable<Client>(), () => seed.CreateTable<Workspace>(),
                     () => seed.CreateTable<EntityTemplate>(), () => seed.CreateTable<BlockDefinition>(),
                     () => seed.CreateTable<FieldDefinition>(), () => seed.CreateTable<EntityRecord>(),
                     () => seed.CreateTable<BlockInstance>(), () => seed.CreateTable<FieldValue>()
                 })
            {
                table();
            }

            var now = DateTime.UtcNow;
            seed.Insert(new Workspace { Id = activeWorkspaceId, Name = "Actif", CreatedAt = now });
            seed.Insert(new Workspace { Id = otherWorkspaceId, Name = "Autre", CreatedAt = now });
            InsertBackupWorkspaceData(seed, activeWorkspaceId, "A");
            InsertBackupWorkspaceData(seed, otherWorkspaceId, "B");
        }

        coordinator = CreateTestCoordinator(root);
        service = new CompleteBackupService(
            coordinator,
            new WorkspaceContextStub(activeWorkspaceId),
            new ClientBackupStub(),
            new DynamicFieldValueSerializer());
        await service.CreateAsync();

        Assert(File.Exists(service.BackupFilePath), "L'archive ZIP n'a pas été créée.");
        Assert(!File.Exists(service.BackupFilePath + ".tmp"), "Le fichier temporaire n'a pas été déplacé/nettoyé.");
        using var archive = ZipFile.OpenRead(service.BackupFilePath);
        var clients = await ReadJsonEntryAsync<List<Client>>(archive, "clients.json");
        var records = await ReadJsonEntryAsync<List<EntityRecord>>(archive, "records.json");
        var templates = await ReadJsonEntryAsync<List<EntityTemplate>>(archive, "templates.json");
        Assert(clients.Count == 1 && clients[0].FirstName == "A",
            $"Le backup complet inclut les clients d'un Workspace étranger. Count={clients.Count}; items={string.Join(';', clients.Select(client => $"{client.FirstName}:{client.WorkspaceId}"))}; expectedWorkspace={activeWorkspaceId}.");
        Assert(records.Count == 1 && records[0].WorkspaceId == activeWorkspaceId, "Le backup complet inclut les records d'un Workspace étranger.");
        Assert(templates.All(template => template.WorkspaceId == activeWorkspaceId), "Le backup complet inclut les templates d'un Workspace étranger.");
    }
    finally
    {
        if (service is not null) await service.DisposeAsync();
        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
            TestCoordinatorRegistry.Remove(coordinator);
        }

        FileSystem.AppDataDirectory = previous;
        DeleteTemporaryDirectory(root);
    }
}

static void InsertBackupWorkspaceData(SQLite.SQLiteConnection connection, Guid workspaceId, string name)
{
    var now = DateTime.UtcNow;
    var client = CreateClient(name, "Client");
    client.WorkspaceId = workspaceId;
    var template = new EntityTemplate { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = name, CreatedAt = now, UpdatedAt = now };
    var block = new BlockDefinition { Id = Guid.NewGuid(), TemplateId = template.Id, Name = "Données", DisplayOrder = 0 };
    var field = new FieldDefinition { Id = Guid.NewGuid(), BlockDefinitionId = block.Id, Label = "Valeur", FieldType = FieldType.ShortText };
    var record = new EntityRecord { Id = Guid.NewGuid(), WorkspaceId = workspaceId, TemplateId = template.Id, ExternalEntityId = client.Id, CreatedAt = now, UpdatedAt = now };
    connection.Insert(client);
    connection.Insert(template);
    connection.Insert(block);
    connection.Insert(field);
    connection.Insert(record);
    connection.Insert(new BlockInstance { Id = Guid.NewGuid(), EntityRecordId = record.Id, BlockDefinitionId = block.Id, Name = block.Name });
    connection.Insert(new FieldValue { Id = Guid.NewGuid(), EntityRecordId = record.Id, FieldDefinitionId = field.Id, ValueJson = "\"" + name + "\"", UpdatedAt = now });
}

static async Task<T> ReadJsonEntryAsync<T>(ZipArchive archive, string entryName)
{
    var entry = archive.GetEntry(entryName) ?? throw new InvalidDataException($"Entrée {entryName} absente de l'archive.");
    await using var stream = entry.Open();
    return await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException($"Entrée {entryName} illisible.");
}

static async Task WithDynamicServiceAsync(Func<SqliteDynamicFormService, Task> test)
{
    var root = CreateTemporaryDirectory();
    var previous = FileSystem.AppDataDirectory;
    SqliteDynamicFormService? service = null;
    SQLiteDatabaseCoordinator? coordinator = null;
    try
    {
        FileSystem.AppDataDirectory = root;
        coordinator = CreateTestCoordinator(root);
        service = new SqliteDynamicFormService(coordinator, new WorkspaceContextStub(Guid.NewGuid()));
        await test(service);
    }
    finally
    {
        if (service is not null)
        {
            await service.DisposeAsync();
        }

        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
            TestCoordinatorRegistry.Remove(coordinator);
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

static async Task TestUsedTemplatesRemainAvailableAsync()
{
    var root = CreateTemporaryDirectory();
    var previous = FileSystem.AppDataDirectory;
    SqliteDynamicFormService? service = null;
    SQLiteDatabaseCoordinator? coordinator = null;
    try
    {
        FileSystem.AppDataDirectory = root;
        var workspaceId = Guid.NewGuid();
        coordinator = CreateTestCoordinator(root);
        service = new SqliteDynamicFormService(coordinator, new WorkspaceContextStub(workspaceId));
        var usedTemplate = await service.CreateTemplateAsync("Modèle utilisé");
        var unusedTemplate = await service.CreateTemplateAsync("Modèle disponible");
        var externalId = Guid.NewGuid();
        var record = await service.GetOrCreateRecordAsync(externalId, usedTemplate.Template.Id);

        var available = await service.GetTemplatesAsync();
        Assert(available.Count == 2, $"2 templates attendus dans le Workspace, obtenu {available.Count}.");
        Assert(available.Any(item => item.Template.Id == usedTemplate.Template.Id), "Le template associé à une fiche doit rester visible.");
        Assert(available.Any(item => item.Template.Id == unusedTemplate.Template.Id), "Le template non utilisé doit rester visible.");
        var selectedAgain = available.Single(item => item.Template.Id == usedTemplate.Template.Id);
        var reused = await service.GetOrCreateRecordAsync(externalId, selectedAgain.Template.Id);
        Assert(reused.Record.Id == record.Record.Id, "Le template utilisé doit rester réutilisable pour la fiche existante.");

        await service.DisposeAsync();
        await coordinator.DisposeAsync();
        TestCoordinatorRegistry.Remove(coordinator);
        coordinator = CreateTestCoordinator(root);
        service = new SqliteDynamicFormService(coordinator, new WorkspaceContextStub(Guid.NewGuid()));
        var otherWorkspaceTemplate = await service.CreateTemplateAsync("Autre Workspace");
        var otherWorkspaceTemplates = await service.GetTemplatesAsync();
        Assert(otherWorkspaceTemplates.Count == 1 && otherWorkspaceTemplates[0].Template.Id == otherWorkspaceTemplate.Template.Id,
            "Les templates d'un autre Workspace ne doivent pas être exposés.");
    }
    finally
    {
        if (service is not null)
        {
            await service.DisposeAsync();
        }

        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
            TestCoordinatorRegistry.Remove(coordinator);
        }

        FileSystem.AppDataDirectory = previous;
        DeleteTemporaryDirectory(root);
    }
}

static async Task TestCompleteBackupRestoreFailsClosedAsync()
{
    var root = CreateTemporaryDirectory();
    var previous = FileSystem.AppDataDirectory;
    CompleteBackupService? service = null;
    SQLiteDatabaseCoordinator? coordinator = null;
    try
    {
        FileSystem.AppDataDirectory = root;
        var databasePath = Path.Combine(root, "ultimate-manager.db3");
        SeedBackupDatabase(databasePath);
        var before = CaptureBackupDatabase(databasePath);
        coordinator = CreateTestCoordinator(root);
        service = new CompleteBackupService(
            coordinator,
            new WorkspaceContextStub(Guid.NewGuid()),
            new ClientBackupStub(),
            new DynamicFieldValueSerializer());

        var failure = await CaptureExceptionAsync(async () => { _ = await service.PickAndRestoreAsync(); });
        Assert(failure is NotSupportedException, $"NotSupportedException attendue, obtenu {failure.GetType().Name}.");
        Assert(failure.Message == ICompleteBackupService.RestoreUnavailableMessage, "Le refus doit expliquer que la restauration est indisponible.");
        await service.DisposeAsync();
        service = null;

        var after = CaptureBackupDatabase(databasePath);
        Assert(before == after, "Une tentative de restauration refusée ne doit modifier aucune ligne préexistante.");
    }
    finally
    {
        if (service is not null)
        {
            await service.DisposeAsync();
        }

        if (coordinator is not null)
        {
            await coordinator.DisposeAsync();
            TestCoordinatorRegistry.Remove(coordinator);
        }

        FileSystem.AppDataDirectory = previous;
        DeleteTemporaryDirectory(root);
    }
}

static void SeedBackupDatabase(string path)
{
    using var connection = new SQLite.SQLiteConnection(path);
    connection.CreateTable<Client>();
    connection.CreateTable<Workspace>();
    connection.CreateTable<EntityTemplate>();
    connection.CreateTable<BlockDefinition>();
    connection.CreateTable<FieldDefinition>();
    connection.CreateTable<EntityRecord>();
    connection.CreateTable<BlockInstance>();
    connection.CreateTable<FieldValue>();
    var now = DateTime.UtcNow;
    var workspace = new Workspace { Id = Guid.NewGuid(), Name = "Préexistant", CreatedAt = now };
    var template = new EntityTemplate { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = "Préexistant", CreatedAt = now, UpdatedAt = now };
    var block = new BlockDefinition { Id = Guid.NewGuid(), TemplateId = template.Id, Name = "Bloc", DisplayOrder = 0 };
    var field = new FieldDefinition { Id = Guid.NewGuid(), BlockDefinitionId = block.Id, Label = "Champ", FieldType = FieldType.ShortText };
    var client = new Client { Id = Guid.NewGuid(), FirstName = "Alice", LastName = "Test", City = "Paris", CreatedAt = now, UpdatedAt = now };
    var record = new EntityRecord { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, TemplateId = template.Id, ExternalEntityId = client.Id, CreatedAt = now, UpdatedAt = now };
    var instance = new BlockInstance { Id = Guid.NewGuid(), EntityRecordId = record.Id, BlockDefinitionId = block.Id, Name = block.Name };
    var value = new FieldValue { Id = Guid.NewGuid(), EntityRecordId = record.Id, FieldDefinitionId = field.Id, ValueJson = "\"à conserver\"", UpdatedAt = now };
    connection.Insert(workspace);
    connection.Insert(template);
    connection.Insert(block);
    connection.Insert(field);
    connection.Insert(client);
    connection.Insert(record);
    connection.Insert(instance);
    connection.Insert(value);
}

static string CaptureBackupDatabase(string path)
{
    using var connection = new SQLite.SQLiteConnection(path);
    return JsonSerializer.Serialize(new
    {
        Clients = connection.Table<Client>().ToList().OrderBy(item => item.Id).ToArray(),
        Workspaces = connection.Table<Workspace>().ToList().OrderBy(item => item.Id).ToArray(),
        Templates = connection.Table<EntityTemplate>().ToList().OrderBy(item => item.Id).ToArray(),
        Blocks = connection.Table<BlockDefinition>().ToList().OrderBy(item => item.Id).ToArray(),
        Fields = connection.Table<FieldDefinition>().ToList().OrderBy(item => item.Id).ToArray(),
        Records = connection.Table<EntityRecord>().ToList().OrderBy(item => item.Id).ToArray(),
        Instances = connection.Table<BlockInstance>().ToList().OrderBy(item => item.Id).ToArray(),
        Values = connection.Table<FieldValue>().ToList().OrderBy(item => item.Id).ToArray()
    });
}
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
        var firstCoordinator = CreateTestCoordinator(root);
        var first = new SqliteDynamicFormService(firstCoordinator, workspace);
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
            await firstCoordinator.DisposeAsync();
            TestCoordinatorRegistry.Remove(firstCoordinator);
        }

        var secondCoordinator = CreateTestCoordinator(root);
        var second = new SqliteDynamicFormService(secondCoordinator, workspace);
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
            await secondCoordinator.DisposeAsync();
            TestCoordinatorRegistry.Remove(secondCoordinator);
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
    var workspace = new WorkspaceContextStub(Guid.NewGuid());
    var backup = new ClientBackupStub();
    var service = new SqliteClientService(coordinator, workspace, backup);
    var dynamicService = new SqliteDynamicFormService(coordinator, workspace);
    try
    {
        await dynamicService.GetTemplatesAsync();
        await test(path, service, backup);
    }
    finally
    {
        await dynamicService.DisposeAsync();
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

static SQLiteDatabaseCoordinator CreateTestCoordinator(string root)
{
    var coordinator = new SQLiteDatabaseCoordinator(Path.Combine(root, "ultimate-manager.db3"));
    TestCoordinatorRegistry.Add(coordinator);
    return coordinator;
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

[SQLite.Table("Clients")]
sealed class LegacyClient
{
    [SQLite.PrimaryKey]
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Address { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

static class TestCoordinatorRegistry
{
    private static readonly List<SQLiteDatabaseCoordinator> Coordinators = [];

    public static void Add(SQLiteDatabaseCoordinator coordinator) => Coordinators.Add(coordinator);

    public static void Remove(SQLiteDatabaseCoordinator coordinator) => Coordinators.Remove(coordinator);

    public static async Task DisposeAllAsync()
    {
        foreach (var coordinator in Coordinators)
        {
            try { await coordinator.DisposeAsync(); } catch { }
        }

        Coordinators.Clear();
    }
}
