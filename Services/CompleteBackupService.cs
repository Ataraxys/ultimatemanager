using System.IO.Compression;
using System.Text.Json;
using SQLite;
using UltimateManager.Models;
using UltimateManager.Models.DynamicForms;

namespace UltimateManager.Services;

public sealed class CompleteBackupService : ICompleteBackupService, IAsyncDisposable
{
    private const int CurrentFormatVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ISqliteDatabase _database;
    private readonly IWorkspaceContext _workspaceContext;
    private readonly IClientBackupService _legacyBackupService;
    private readonly DynamicFieldValueSerializer _valueSerializer;
    private readonly Lazy<Task> _initializationTask;
    private readonly SemaphoreSlim _backupLock = new(1, 1);

    public CompleteBackupService(
        ISqliteDatabase database,
        IWorkspaceContext workspaceContext,
        IClientBackupService legacyBackupService,
        DynamicFieldValueSerializer valueSerializer)
    {
        _database = database;
        _workspaceContext = workspaceContext;
        _legacyBackupService = legacyBackupService;
        _valueSerializer = valueSerializer;
        _initializationTask = new Lazy<Task>(InitializeAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string BackupFilePath => Path.Combine(FileSystem.AppDataDirectory, "UltimateManager-backup.zip");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        await _backupLock.WaitAsync(cancellationToken);
        var temporaryPath = BackupFilePath + ".tmp";
        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var snapshot = await _database.ReadAsync(async connection =>
            {
                var workspace = await connection.FindAsync<Workspace>(_workspaceContext.WorkspaceId)
                    ?? throw new InvalidOperationException("L’espace de travail est introuvable.");
                var clients = await connection.Table<Client>()
                    .Where(item => item.WorkspaceId == _workspaceContext.WorkspaceId)
                    .ToListAsync();
                var templates = await connection.Table<EntityTemplate>()
                    .Where(item => item.WorkspaceId == _workspaceContext.WorkspaceId)
                    .ToListAsync();
                var templateIds = templates.Select(item => item.Id).ToHashSet();
                var blocks = (await connection.Table<BlockDefinition>().ToListAsync())
                    .Where(item => templateIds.Contains(item.TemplateId)).ToList();
                var blockIds = blocks.Select(item => item.Id).ToHashSet();
                var fields = (await connection.Table<FieldDefinition>().ToListAsync())
                    .Where(item => blockIds.Contains(item.BlockDefinitionId)).ToList();
                var fieldIds = fields.Select(item => item.Id).ToHashSet();
                var records = await connection.Table<EntityRecord>()
                    .Where(item => item.WorkspaceId == _workspaceContext.WorkspaceId)
                    .ToListAsync();
                var recordIds = records.Select(item => item.Id).ToHashSet();
                var recordBlocks = (await connection.Table<BlockInstance>().ToListAsync())
                    .Where(item => recordIds.Contains(item.EntityRecordId)).ToList();
                var values = (await connection.Table<FieldValue>().ToListAsync())
                    .Where(item => recordIds.Contains(item.EntityRecordId) && fieldIds.Contains(item.FieldDefinitionId)).ToList();
                return new BackupSnapshot(workspace, clients, templates, blocks, fields, records, recordBlocks, values);
            }, cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(BackupFilePath)!);
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            await using (var fileStream = File.Create(temporaryPath))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                await WriteJsonAsync(archive, "manifest.json", new BackupManifest
                {
                    FormatVersion = CurrentFormatVersion,
                    CreatedAt = DateTime.UtcNow,
                    WorkspaceId = snapshot.Workspace.Id
                }, cancellationToken);
                await WriteJsonAsync(archive, "workspace.json", snapshot.Workspace, cancellationToken);
                await WriteJsonAsync(archive, "clients.json", snapshot.Clients, cancellationToken);
                await WriteJsonAsync(archive, "templates.json", snapshot.Templates, cancellationToken);
                await WriteJsonAsync(archive, "blocks.json", snapshot.Blocks, cancellationToken);
                await WriteJsonAsync(archive, "fields.json", snapshot.Fields, cancellationToken);
                await WriteJsonAsync(archive, "records.json", snapshot.Records, cancellationToken);
                await WriteJsonAsync(archive, "record-blocks.json", snapshot.RecordBlocks, cancellationToken);

                var exportedValues = new List<FieldValue>(snapshot.Values.Count);
                var attachments = new List<(string EntryName, string FilePath)>();
                var fieldTypes = snapshot.Fields.ToDictionary(field => field.Id, field => field.FieldType);
                foreach (var value in snapshot.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var exportedValue = new FieldValue
                    {
                        Id = value.Id,
                        EntityRecordId = value.EntityRecordId,
                        FieldDefinitionId = value.FieldDefinitionId,
                        ValueJson = value.ValueJson,
                        UpdatedAt = value.UpdatedAt
                    };
                    if (fieldTypes.GetValueOrDefault(value.FieldDefinitionId) == FieldType.Document)
                    {
                        var document = _valueSerializer.Deserialize<DocumentValue>(value.ValueJson);
                        if (document is null)
                        {
                            throw new InvalidDataException("Une valeur de document est invalide et ne peut pas être sauvegardée.");
                        }

                        if (!File.Exists(document.FilePath))
                        {
                            throw new FileNotFoundException(
                                $"Le document référencé « {document.FileName} » est absent; la sauvegarde complète est annulée.",
                                document.FilePath);
                        }

                        var entryName = $"attachments/{Guid.NewGuid():N}_{SanitizeFileName(document.FileName)}";
                        exportedValue.ValueJson = _valueSerializer.Serialize(new DocumentValue
                        {
                            FileName = document.FileName,
                            ContentType = document.ContentType,
                            FilePath = entryName
                        });
                        attachments.Add((entryName, document.FilePath));
                    }
                    exportedValues.Add(exportedValue);
                }

                await WriteJsonAsync(archive, "values.json", exportedValues, cancellationToken);
                foreach (var attachment in attachments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = archive.CreateEntry(attachment.EntryName, CompressionLevel.Fastest);
                    await using var target = entry.Open();
                    await using var source = File.OpenRead(attachment.FilePath);
                    await source.CopyToAsync(target, cancellationToken);
                }
            }

            File.Move(temporaryPath, BackupFilePath, true);
            await _legacyBackupService.RegenerateAsync(snapshot.Clients, cancellationToken);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _backupLock.Release();
        }
    }

    public async Task CreateAndShareAsync(CancellationToken cancellationToken = default)
    {
        await CreateAsync(cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Sauvegarde complète UltimateManager",
            File = new ShareFile(BackupFilePath)
        });
    }

    public Task<bool> PickAndRestoreAsync(CancellationToken cancellationToken = default) =>
        Task.FromException<bool>(new NotSupportedException(ICompleteBackupService.RestoreUnavailableMessage));

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await _workspaceContext.EnsureInitializedAsync(cancellationToken);
        await _initializationTask.Value.WaitAsync(cancellationToken);
    }

    private async Task InitializeAsync() => await _database.InitializeAsync();

    private static async Task WriteJsonAsync<T>(ZipArchive archive, string name, T value, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized;
    }

    private sealed class BackupManifest
    {
        public int FormatVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid WorkspaceId { get; set; }
    }

    private sealed record BackupSnapshot(
        Workspace Workspace,
        List<Client> Clients,
        List<EntityTemplate> Templates,
        List<BlockDefinition> Blocks,
        List<FieldDefinition> Fields,
        List<EntityRecord> Records,
        List<BlockInstance> RecordBlocks,
        List<FieldValue> Values);
}
