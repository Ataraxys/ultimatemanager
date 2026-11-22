using UltimateManager.Models.DynamicForms;

namespace UltimateManager.Services;

public interface IDynamicFormService
{
    Task<IReadOnlyList<TemplateDefinition>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<TemplateDefinition?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);

    Task<EntityRecordModel?> GetRecordForExternalEntityAsync(Guid externalEntityId, CancellationToken cancellationToken = default);

    Task<TemplateDefinition> CloneTemplateAsync(Guid sourceTemplateId, string name, CancellationToken cancellationToken = default);

    Task<TemplateDefinition> EnsureClientTemplateAsync(Guid externalEntityId, Guid sourceTemplateId, CancellationToken cancellationToken = default);

    Task<TemplateDefinition> CreateTemplateAsync(string name, string entityType = "Client", CancellationToken cancellationToken = default);

    Task<TemplateDefinition> EnsureDefaultClientTemplateAsync(CancellationToken cancellationToken = default);

    Task<TemplateDefinition> GetDefaultClientTemplateAsync(CancellationToken cancellationToken = default);

    Task SetDefaultTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);

    Task DeleteTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);

    Task<BlockDefinition> AddBlockAsync(Guid templateId, string name, CancellationToken cancellationToken = default);

    Task<FieldDefinition> AddFieldAsync(Guid blockId, string label, FieldType fieldType, CancellationToken cancellationToken = default);

    Task UpdateBlockAsync(BlockDefinition block, CancellationToken cancellationToken = default);

    Task UpdateFieldAsync(FieldDefinition field, CancellationToken cancellationToken = default);

    Task DeleteBlockAsync(Guid blockId, CancellationToken cancellationToken = default);

    Task DeleteFieldAsync(Guid fieldId, CancellationToken cancellationToken = default);

    Task MoveBlockAsync(Guid templateId, Guid blockId, int offset, CancellationToken cancellationToken = default);

    Task MoveFieldAsync(Guid blockId, Guid fieldId, int offset, CancellationToken cancellationToken = default);

    Task SaveFieldValueAsync(Guid recordId, Guid fieldDefinitionId, string valueJson, CancellationToken cancellationToken = default);

    Task<EntityRecordModel> CreateRecordAsync(Guid templateId, CancellationToken cancellationToken = default);

    Task<EntityRecordModel> GetOrCreateRecordAsync(Guid externalEntityId, Guid templateId, CancellationToken cancellationToken = default);

    Task<EntityRecordModel?> GetRecordAsync(Guid recordId, CancellationToken cancellationToken = default);
}
