using UltimateManager.Models.DynamicForms;
using UltimateManager.Models;
using UltimateManager.Services;

internal sealed class DynamicFormServiceStub : IDynamicFormService
{
    public List<EntityTemplate> PersistedTemplates { get; } = [];
    public List<EntityRecord> PersistedRecords { get; } = [];
    public int PersistenceWrites { get; private set; }
    public Guid ActualTemplateId { get; set; } = Guid.NewGuid();
    public Guid LastLoadedTemplateId { get; private set; }

    public Task<IReadOnlyList<TemplateDefinition>> GetTemplatesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TemplateDefinition>>([]);

    public Task<TemplateDefinition?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) =>
        Task.FromResult<TemplateDefinition?>(new TemplateDefinition { Template = new EntityTemplate { Id = templateId, Name = "Persisté", EntityType = "Client" } });

    public Task<EntityRecordModel?> GetRecordForExternalEntityAsync(Guid externalEntityId, CancellationToken cancellationToken = default) =>
        Task.FromResult<EntityRecordModel?>(null);

    public Task<TemplateDefinition> CloneTemplateAsync(Guid sourceTemplateId, string name, CancellationToken cancellationToken = default, Guid? ownerClientId = null) =>
        Task.FromResult(new TemplateDefinition());

    public Task<TemplateDefinition> EnsureClientTemplateAsync(Guid externalEntityId, Guid sourceTemplateId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new TemplateDefinition());

    public Task<EntityRecordModel> CreateClientWithDraftAsync(
        UltimateManager.Models.Client client,
        TemplateDefinition draft,
        IReadOnlyDictionary<Guid, string> fieldValues,
        CancellationToken cancellationToken = default)
    {
        PersistenceWrites++;
        PersistedTemplates.Add(draft.Template);
        var templateId = ActualTemplateId;
        var record = new EntityRecord { Id = Guid.NewGuid(), TemplateId = templateId, ExternalEntityId = client.Id };
        PersistedRecords.Add(record);
        return Task.FromResult(new EntityRecordModel { Record = record });
    }

    public Task<EntityRecordModel> GetOrCreateRecordAsync(Guid externalEntityId, Guid templateId, CancellationToken cancellationToken = default)
    {
        LastLoadedTemplateId = templateId;
        return Task.FromResult(new EntityRecordModel { Record = new EntityRecord { Id = Guid.NewGuid(), TemplateId = templateId, ExternalEntityId = externalEntityId } });
    }

    public Task<TemplateDefinition> CreateTemplateAsync(string name, string entityType = "Client", CancellationToken cancellationToken = default) =>
        Task.FromResult(new TemplateDefinition());

    public Task<TemplateDefinition> EnsureDefaultClientTemplateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new TemplateDefinition());

    public Task<TemplateDefinition> GetDefaultClientTemplateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new TemplateDefinition());

    public Task SetDefaultTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<BlockDefinition> AddBlockAsync(Guid templateId, string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BlockDefinition());

    public Task<FieldDefinition> AddFieldAsync(Guid blockId, string label, FieldType fieldType, CancellationToken cancellationToken = default) =>
        Task.FromResult(new FieldDefinition());

    public Task UpdateBlockAsync(BlockDefinition block, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateFieldAsync(FieldDefinition field, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteBlockAsync(Guid blockId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteFieldAsync(Guid fieldId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task MoveBlockAsync(Guid templateId, Guid blockId, int offset, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task MoveFieldAsync(Guid blockId, Guid fieldId, int offset, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SaveFieldValueAsync(Guid recordId, Guid fieldDefinitionId, string valueJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SaveFieldValuesAsync(Guid recordId, IReadOnlyDictionary<Guid, string> values, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<EntityRecordModel> CreateRecordAsync(Guid templateId, CancellationToken cancellationToken = default) => Task.FromResult(new EntityRecordModel());
    public Task<EntityRecordModel?> GetRecordAsync(Guid recordId, CancellationToken cancellationToken = default) => Task.FromResult<EntityRecordModel?>(null);
}

internal sealed class ClientServiceStub : IClientService
{
    public Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Client>>([]);
    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Client?>(null);
    public Task<Client> CreateAsync(Client client, CancellationToken cancellationToken = default) => Task.FromResult(client);
    public Task<Client> UpdateAsync(Client client, CancellationToken cancellationToken = default) => Task.FromResult(client);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<Client>> SearchAsync(string searchText, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Client>>([]);
}

internal sealed class MapProviderStub : IMapProvider
{
    public Task<LocationResult?> PickAsync(LocationResult? initialLocation = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<LocationResult?>(null);
}

internal sealed class AutomaticBackupStub : IAutomaticBackupService
{
    public Task SaveAfterChangeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
