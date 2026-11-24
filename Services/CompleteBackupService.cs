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

    private readonly SQLiteAsyncConnection _connection;
    private readonly IWorkspaceContext _workspaceContext;
    private readonly IClientBackupService _legacyBackupService;
    private readonly DynamicFieldValueSerializer _valueSerializer;
    private readonly Lazy<Task> _initializationTask;

    public CompleteBackupService(
        IWorkspaceContext workspaceContext,
        IClientBackupService legacyBackupService,
        DynamicFieldValueSerializer valueSerializer)
    {
        _workspaceContext = workspaceContext;
        _legacyBackupService = legacyBackupService;
        _valueSerializer = valueSerializer;
        _connection = new SQLiteAsyncConnection(Path.Combine(FileSystem.AppDataDirectory, "ultimate-manager.db3"));
        _initializationTask = new Lazy<Task>(InitializeAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string BackupFilePath => Path.Combine(FileSystem.AppDataDirectory, "UltimateManager-backup.zip");

    public async ValueTask DisposeAsync()
    {
        if (!_initializationTask.IsValueCreated)
        {
            return;
        }

        try
        {
            await _initializationTask.Value;
        }
        finally
        {
            await _connection.CloseAsync();
        }
    }

    public async Task CreateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var workspace = await _connection.FindAsync<Workspace>(_workspaceContext.WorkspaceId)
            ?? throw new InvalidOperationException("L’espace de travail est introuvable.");
        var clients = await _connection.Table<Client>().ToListAsync();
        var templates = await _connection.Table<EntityTemplate>()
            .Where(item => item.WorkspaceId == _workspaceContext.WorkspaceId)
            .ToListAsync();
        var templateIds = templates.Select(item => item.Id).ToHashSet();
        var blocks = await _connection.Table<BlockDefinition>().ToListAsync();
        blocks = blocks.Where(item => templateIds.Contains(item.TemplateId)).ToList();
        var blockIds = blocks.Select(item => item.Id).ToHashSet();
        var fields = await _connection.Table<FieldDefinition>().ToListAsync();
        fields = fields.Where(item => blockIds.Contains(item.BlockDefinitionId)).ToList();
        var records = await _connection.Table<EntityRecord>()
            .Where(item => item.WorkspaceId == _workspaceContext.WorkspaceId)
            .ToListAsync();
        var recordIds = records.Select(item => item.Id).ToHashSet();
        var recordBlocks = await _connection.Table<BlockInstance>().ToListAsync();
        recordBlocks = recordBlocks.Where(item => recordIds.Contains(item.EntityRecordId)).ToList();
        var values = await _connection.Table<FieldValue>().ToListAsync();
        values = values.Where(item => recordIds.Contains(item.EntityRecordId) && fields.Any(field => field.Id == item.FieldDefinitionId)).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(BackupFilePath)!);
        var temporaryPath = BackupFilePath + ".tmp";
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
                WorkspaceId = workspace.Id
            }, cancellationToken);
            await WriteJsonAsync(archive, "workspace.json", workspace, cancellationToken);
            await WriteJsonAsync(archive, "clients.json", clients, cancellationToken);
            await WriteJsonAsync(archive, "templates.json", templates, cancellationToken);
            await WriteJsonAsync(archive, "blocks.json", blocks, cancellationToken);
            await WriteJsonAsync(archive, "fields.json", fields, cancellationToken);
            await WriteJsonAsync(archive, "records.json", records, cancellationToken);
            await WriteJsonAsync(archive, "record-blocks.json", recordBlocks, cancellationToken);

            var exportedValues = new List<FieldValue>(values.Count);
            var attachments = new List<(string EntryName, string FilePath)>();
            var fieldTypes = fields.ToDictionary(field => field.Id, field => field.FieldType);
            foreach (var value in values)
            {
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
                    if (document is not null && File.Exists(document.FilePath))
                    {
                        var entryName = $"attachments/{Guid.NewGuid():N}_{SanitizeFileName(document.FileName)}";
                        var archiveDocument = new DocumentValue
                        {
                            FileName = document.FileName,
                            ContentType = document.ContentType,
                            FilePath = entryName
                        };
                        exportedValue.ValueJson = _valueSerializer.Serialize(archiveDocument);
                        attachments.Add((entryName, document.FilePath));
                    }
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
        await _legacyBackupService.RegenerateAsync(clients, cancellationToken);
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

    private async Task InitializeAsync()
    {
        await _connection.CreateTableAsync<Client>();
        await _connection.CreateTableAsync<Workspace>();
        await _connection.CreateTableAsync<EntityTemplate>();
        await _connection.CreateTableAsync<BlockDefinition>();
        await _connection.CreateTableAsync<FieldDefinition>();
        await _connection.CreateTableAsync<EntityRecord>();
        await _connection.CreateTableAsync<BlockInstance>();
        await _connection.CreateTableAsync<FieldValue>();
    }

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
}
