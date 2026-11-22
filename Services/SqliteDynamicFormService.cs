using SQLite;
using UltimateManager.Models.DynamicForms;

namespace UltimateManager.Services;

public sealed class SqliteDynamicFormService : IDynamicFormService
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly IWorkspaceContext _workspaceContext;
    private readonly Task _initializationTask;

    public SqliteDynamicFormService(IWorkspaceContext workspaceContext)
    {
        _workspaceContext = workspaceContext;
        _connection = new SQLiteAsyncConnection(Path.Combine(FileSystem.AppDataDirectory, "ultimate-manager.db3"));
        _initializationTask = InitializeAsync();
    }

    public async Task<IReadOnlyList<TemplateDefinition>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var templates = await _connection.Table<EntityTemplate>()
            .Where(template => template.WorkspaceId == _workspaceContext.WorkspaceId)
            .OrderBy(template => template.Name)
            .ToListAsync();
        var clientTemplateIds = (await _connection.Table<EntityRecord>()
                .Where(record => record.WorkspaceId == _workspaceContext.WorkspaceId)
                .ToListAsync())
            .Select(record => record.TemplateId)
            .ToHashSet();

        var result = new List<TemplateDefinition>(Math.Max(templates.Count - clientTemplateIds.Count, 0));
        foreach (var template in templates.Where(template => !clientTemplateIds.Contains(template.Id)))
        {
            result.Add(await LoadTemplateAsync(template, cancellationToken));
        }

        return result;
    }

    public async Task<TemplateDefinition?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var template = await _connection.FindAsync<EntityTemplate>(templateId);
        return template is null || template.WorkspaceId != _workspaceContext.WorkspaceId
            ? null
            : await LoadTemplateAsync(template, cancellationToken);
    }

    public async Task<EntityRecordModel?> GetRecordForExternalEntityAsync(
        Guid externalEntityId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var record = await _connection.Table<EntityRecord>()
            .Where(item => item.WorkspaceId == _workspaceContext.WorkspaceId &&
                           item.ExternalEntityId == externalEntityId)
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync();
        return record is null ? null : await GetRecordAsync(record.Id, cancellationToken);
    }

    public async Task<TemplateDefinition> CloneTemplateAsync(
        Guid sourceTemplateId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var source = await GetTemplateAsync(sourceTemplateId, cancellationToken)
            ?? throw new InvalidOperationException("Le template source est introuvable.");
        var clone = await CreateTemplateAsync(name, source.Template.EntityType, cancellationToken);
        foreach (var sourceBlock in source.Blocks)
        {
            var block = await AddBlockAsync(clone.Template.Id, sourceBlock.Block.Name, cancellationToken);
            foreach (var sourceField in sourceBlock.Fields)
            {
                var field = await AddFieldAsync(block.Id, sourceField.Label, sourceField.FieldType, cancellationToken);
                field.IsRequired = sourceField.IsRequired;
                field.OptionsJson = sourceField.OptionsJson;
                await UpdateFieldAsync(field, cancellationToken);
            }
        }

        return await GetTemplateAsync(clone.Template.Id, cancellationToken)
            ?? throw new InvalidOperationException("Le template cloné est introuvable.");
    }

    public async Task<TemplateDefinition> EnsureClientTemplateAsync(
        Guid externalEntityId,
        Guid sourceTemplateId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var existingRecord = await _connection.Table<EntityRecord>()
            .Where(record => record.WorkspaceId == _workspaceContext.WorkspaceId &&
                             record.ExternalEntityId == externalEntityId)
            .OrderByDescending(record => record.UpdatedAt)
            .FirstOrDefaultAsync();
        if (existingRecord is not null)
        {
            return await GetTemplateAsync(existingRecord.TemplateId, cancellationToken)
                ?? throw new InvalidOperationException("Le template de la fiche client est introuvable.");
        }

        var clientTemplate = sourceTemplateId == Guid.Empty
            ? await CreateTemplateAsync($"Fiche de {externalEntityId:N}", "Client", cancellationToken)
            : await CloneTemplateAsync(
                sourceTemplateId,
                $"Fiche de {externalEntityId:N}",
                cancellationToken);
        await GetOrCreateRecordAsync(externalEntityId, clientTemplate.Template.Id, cancellationToken);
        return clientTemplate;
    }

    public async Task<TemplateDefinition> CreateTemplateAsync(
        string name,
        string entityType = "Client",
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var template = new EntityTemplate
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceContext.WorkspaceId,
            Name = name.Trim(),
            EntityType = entityType,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _connection.InsertAsync(template);
        return await LoadTemplateAsync(template, cancellationToken);
    }

    public async Task<TemplateDefinition> EnsureDefaultClientTemplateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var existing = await _connection.Table<EntityTemplate>()
            .Where(template => template.WorkspaceId == _workspaceContext.WorkspaceId &&
                               template.EntityType == "Client" &&
                               template.Name == "Fiche client")
            .FirstOrDefaultAsync();
        if (existing is not null)
        {
            await SetDefaultTemplateIfEmptyAsync(existing.Id);
            return await LoadTemplateAsync(existing, cancellationToken);
        }

        var templateDefinition = await CreateTemplateAsync("Fiche client", "Client", cancellationToken);
        var blocks = new[]
        {
            (Name: "Informations personnelles", Fields: new[]
            {
                ("Prénom", FieldType.ShortText), ("Nom", FieldType.ShortText)
            }),
            (Name: "Coordonnées", Fields: new[]
            {
                ("Téléphone", FieldType.Phone), ("Email", FieldType.Email), ("Adresse", FieldType.Address)
            }),
            (Name: "Informations complémentaires", Fields: new[]
            {
                ("Commentaire", FieldType.Comment)
            })
        };

        var blockOrder = 0;
        foreach (var blockDefinition in blocks)
        {
            var block = new BlockDefinition
            {
                Id = Guid.NewGuid(),
                TemplateId = templateDefinition.Template.Id,
                Name = blockDefinition.Name,
                DisplayOrder = blockOrder++
            };
            await _connection.InsertAsync(block);
            var fieldOrder = 0;
            foreach (var fieldDefinition in blockDefinition.Fields)
            {
                await _connection.InsertAsync(new FieldDefinition
                {
                    Id = Guid.NewGuid(),
                    BlockDefinitionId = block.Id,
                    Label = fieldDefinition.Item1,
                    FieldType = fieldDefinition.Item2,
                    DisplayOrder = fieldOrder++
                });
            }
        }

        await SetDefaultTemplateIfEmptyAsync(templateDefinition.Template.Id);
        return await LoadTemplateAsync(templateDefinition.Template, cancellationToken);
    }

    public async Task<TemplateDefinition> GetDefaultClientTemplateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var workspace = await _connection.FindAsync<Workspace>(_workspaceContext.WorkspaceId);
        if (workspace is not null && workspace.DefaultTemplateId != Guid.Empty)
        {
            var template = await GetTemplateAsync(workspace.DefaultTemplateId, cancellationToken);
            if (template?.Template.EntityType == "Client")
            {
                return template;
            }
        }

        return await EnsureDefaultClientTemplateAsync(cancellationToken);
    }

    public async Task SetDefaultTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var template = await GetTemplateAsync(templateId, cancellationToken)
            ?? throw new InvalidOperationException("Le modèle demandé n’existe pas dans cet espace de travail.");
        if (template.Template.EntityType != "Client")
        {
            throw new InvalidOperationException("Seuls les modèles Client peuvent être définis par défaut pour les clients.");
        }

        var workspace = await _connection.FindAsync<Workspace>(_workspaceContext.WorkspaceId)
            ?? throw new InvalidOperationException("L’espace de travail est introuvable.");
        workspace.DefaultTemplateId = templateId;
        await _connection.UpdateAsync(workspace);
    }

    private async Task SetDefaultTemplateIfEmptyAsync(Guid templateId)
    {
        var workspace = await _connection.FindAsync<Workspace>(_workspaceContext.WorkspaceId);
        if (workspace is null || workspace.DefaultTemplateId != Guid.Empty)
        {
            return;
        }

        workspace.DefaultTemplateId = templateId;
        await _connection.UpdateAsync(workspace);
    }

    public async Task DeleteTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var template = await _connection.FindAsync<EntityTemplate>(templateId);
        if (template is null || template.WorkspaceId != _workspaceContext.WorkspaceId)
        {
            throw new InvalidOperationException("Le modèle demandé n’existe pas dans cet espace de travail.");
        }

        var workspace = await _connection.FindAsync<Workspace>(_workspaceContext.WorkspaceId);
        if (workspace?.DefaultTemplateId == templateId)
        {
            throw new InvalidOperationException("Le modèle par défaut ne peut pas être supprimé.");
        }

        var isUsedByClient = await _connection.Table<EntityRecord>()
            .Where(record => record.WorkspaceId == _workspaceContext.WorkspaceId && record.TemplateId == templateId)
            .FirstOrDefaultAsync() is not null;
        if (isUsedByClient)
        {
            throw new InvalidOperationException("Ce modèle est utilisé par une fiche client et ne peut pas être supprimé.");
        }

        var blocks = await _connection.Table<BlockDefinition>()
            .Where(block => block.TemplateId == templateId)
            .ToListAsync();
        foreach (var block in blocks)
        {
            var fields = await _connection.Table<FieldDefinition>()
                .Where(field => field.BlockDefinitionId == block.Id)
                .ToListAsync();
            foreach (var field in fields)
            {
                await _connection.DeleteAsync(field);
            }

            await _connection.DeleteAsync(block);
        }

        await _connection.DeleteAsync(template);
    }

    public async Task<BlockDefinition> AddBlockAsync(
        Guid templateId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateAsync(templateId, cancellationToken)
            ?? throw new InvalidOperationException("Le modèle demandé n’existe pas dans cet espace de travail.");
        var block = new BlockDefinition
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Template.Id,
            Name = name.Trim(),
            DisplayOrder = template.Blocks.Count
        };
        await _connection.InsertAsync(block);
        await TouchTemplateAsync(template.Template.Id);
        return block;
    }

    public async Task<FieldDefinition> AddFieldAsync(
        Guid blockId,
        string label,
        FieldType fieldType,
        CancellationToken cancellationToken = default)
    {
        var block = await GetBlockInWorkspaceAsync(blockId, cancellationToken)
            ?? throw new InvalidOperationException("Le bloc demandé n’existe pas dans cet espace de travail.");
        var fieldCount = await _connection.Table<FieldDefinition>()
            .Where(field => field.BlockDefinitionId == block.Id)
            .CountAsync();
        var field = new FieldDefinition
        {
            Id = Guid.NewGuid(),
            BlockDefinitionId = block.Id,
            Label = label.Trim(),
            FieldType = fieldType,
            DisplayOrder = fieldCount
        };
        await _connection.InsertAsync(field);
        await TouchTemplateForBlockAsync(block.Id);
        return field;
    }

    public async Task UpdateBlockAsync(BlockDefinition block, CancellationToken cancellationToken = default)
    {
        var existing = await GetBlockInWorkspaceAsync(block.Id, cancellationToken)
            ?? throw new InvalidOperationException("Le bloc demandé n’existe pas dans cet espace de travail.");
        existing.Name = block.Name.Trim();
        existing.DisplayOrder = block.DisplayOrder;
        await _connection.UpdateAsync(existing);
        await TouchTemplateForBlockAsync(existing.Id);
    }

    public async Task UpdateFieldAsync(FieldDefinition field, CancellationToken cancellationToken = default)
    {
        var existing = await GetFieldInWorkspaceAsync(field.Id, cancellationToken)
            ?? throw new InvalidOperationException("Le champ demandé n’existe pas dans cet espace de travail.");
        existing.Label = field.Label.Trim();
        existing.FieldType = field.FieldType;
        existing.IsRequired = field.IsRequired;
        existing.OptionsJson = field.OptionsJson;
        existing.DisplayOrder = field.DisplayOrder;
        await _connection.UpdateAsync(existing);
        await TouchTemplateForFieldAsync(existing.Id);
    }

    public async Task DeleteBlockAsync(Guid blockId, CancellationToken cancellationToken = default)
    {
        var block = await GetBlockInWorkspaceAsync(blockId, cancellationToken)
            ?? throw new InvalidOperationException("Le bloc demandé n’existe pas dans cet espace de travail.");
        var fields = await _connection.Table<FieldDefinition>()
            .Where(field => field.BlockDefinitionId == block.Id)
            .ToListAsync();
        foreach (var field in fields)
        {
            await _connection.DeleteAsync(field);
        }

        await _connection.DeleteAsync(block);
        await NormalizeBlockOrderAsync(block.TemplateId);
        await TouchTemplateAsync(block.TemplateId);
    }

    public async Task DeleteFieldAsync(Guid fieldId, CancellationToken cancellationToken = default)
    {
        var field = await GetFieldInWorkspaceAsync(fieldId, cancellationToken)
            ?? throw new InvalidOperationException("Le champ demandé n’existe pas dans cet espace de travail.");
        await _connection.DeleteAsync(field);
        await NormalizeFieldOrderAsync(field.BlockDefinitionId);
        await TouchTemplateForBlockAsync(field.BlockDefinitionId);
    }

    public async Task MoveBlockAsync(Guid templateId, Guid blockId, int offset, CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateAsync(templateId, cancellationToken)
            ?? throw new InvalidOperationException("Le modèle demandé n’existe pas dans cet espace de travail.");
        var blocks = template.Blocks.Select(model => model.Block).ToList();
        MoveItem<BlockDefinition>(blocks, block => block.Id == blockId, offset);
        for (var index = 0; index < blocks.Count; index++)
        {
            blocks[index].DisplayOrder = index;
            await _connection.UpdateAsync(blocks[index]);
        }
        await TouchTemplateAsync(templateId);
    }

    public async Task MoveFieldAsync(Guid blockId, Guid fieldId, int offset, CancellationToken cancellationToken = default)
    {
        var block = await GetBlockInWorkspaceAsync(blockId, cancellationToken)
            ?? throw new InvalidOperationException("Le bloc demandé n’existe pas dans cet espace de travail.");
        var fields = await _connection.Table<FieldDefinition>()
            .Where(field => field.BlockDefinitionId == block.Id)
            .OrderBy(field => field.DisplayOrder)
            .ToListAsync();
        MoveItem<FieldDefinition>(fields, field => field.Id == fieldId, offset);
        for (var index = 0; index < fields.Count; index++)
        {
            fields[index].DisplayOrder = index;
            await _connection.UpdateAsync(fields[index]);
        }
        await TouchTemplateForBlockAsync(block.Id);
    }

    public async Task SaveFieldValueAsync(
        Guid recordId,
        Guid fieldDefinitionId,
        string valueJson,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var record = await _connection.FindAsync<EntityRecord>(recordId);
        var field = await GetFieldInWorkspaceAsync(fieldDefinitionId, cancellationToken);
        if (record is null || record.WorkspaceId != _workspaceContext.WorkspaceId || field is null)
        {
            throw new InvalidOperationException("La fiche ou le champ demandé n’existe pas dans cet espace de travail.");
        }

        var value = await _connection.Table<FieldValue>()
            .Where(item => item.EntityRecordId == recordId && item.FieldDefinitionId == fieldDefinitionId)
            .FirstOrDefaultAsync();
        if (value is null)
        {
            await _connection.InsertAsync(new FieldValue
            {
                Id = Guid.NewGuid(),
                EntityRecordId = recordId,
                FieldDefinitionId = fieldDefinitionId,
                ValueJson = valueJson,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            value.ValueJson = valueJson;
            value.UpdatedAt = DateTime.UtcNow;
            await _connection.UpdateAsync(value);
        }

        record.UpdatedAt = DateTime.UtcNow;
        await _connection.UpdateAsync(record);
    }

    public async Task<EntityRecordModel> CreateRecordAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateAsync(templateId, cancellationToken)
            ?? throw new InvalidOperationException("Le modèle demandé n’existe pas dans cet espace de travail.");
        var now = DateTime.UtcNow;
        var record = new EntityRecord
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceContext.WorkspaceId,
            TemplateId = template.Template.Id,
            EntityType = template.Template.EntityType,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _connection.InsertAsync(record);

        var blocks = new List<BlockInstanceModel>(template.Blocks.Count);
        foreach (var blockDefinition in template.Blocks)
        {
            var block = new BlockInstance
            {
                Id = Guid.NewGuid(),
                EntityRecordId = record.Id,
                BlockDefinitionId = blockDefinition.Block.Id,
                Name = blockDefinition.Block.Name,
                DisplayOrder = blockDefinition.Block.DisplayOrder
            };
            await _connection.InsertAsync(block);
            blocks.Add(new BlockInstanceModel { Block = block, Values = [] });
        }

        return new EntityRecordModel { Record = record, Blocks = blocks };
    }

    public async Task<EntityRecordModel> GetOrCreateRecordAsync(
        Guid externalEntityId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var existing = await _connection.Table<EntityRecord>()
            .Where(record => record.WorkspaceId == _workspaceContext.WorkspaceId &&
                             record.ExternalEntityId == externalEntityId &&
                             record.TemplateId == templateId)
            .FirstOrDefaultAsync();
        if (existing is not null)
        {
            return await GetRecordAsync(existing.Id, cancellationToken)
                ?? throw new InvalidOperationException("La fiche dynamique existante est inaccessible.");
        }

        var previousRecord = await _connection.Table<EntityRecord>()
            .Where(record => record.WorkspaceId == _workspaceContext.WorkspaceId &&
                             record.ExternalEntityId == externalEntityId)
            .FirstOrDefaultAsync();
        if (previousRecord is not null)
        {
            await DeleteRecordDataAsync(previousRecord.Id);
        }

        var template = await GetTemplateAsync(templateId, cancellationToken)
            ?? throw new InvalidOperationException("Le modèle demandé n’existe pas dans cet espace de travail.");
        var now = DateTime.UtcNow;
        var record = new EntityRecord
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceContext.WorkspaceId,
            TemplateId = templateId,
            ExternalEntityId = externalEntityId,
            EntityType = template.Template.EntityType,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _connection.InsertAsync(record);

        var blocks = new List<BlockInstanceModel>(template.Blocks.Count);
        foreach (var definition in template.Blocks)
        {
            var block = new BlockInstance
            {
                Id = Guid.NewGuid(),
                EntityRecordId = record.Id,
                BlockDefinitionId = definition.Block.Id,
                Name = definition.Block.Name,
                DisplayOrder = definition.Block.DisplayOrder
            };
            await _connection.InsertAsync(block);
            blocks.Add(new BlockInstanceModel { Block = block, Values = [] });
        }

        return new EntityRecordModel { Record = record, Blocks = blocks };
    }

    private async Task DeleteRecordDataAsync(Guid recordId)
    {
        var values = await _connection.Table<FieldValue>()
            .Where(value => value.EntityRecordId == recordId)
            .ToListAsync();
        foreach (var value in values)
        {
            await _connection.DeleteAsync(value);
        }

        var blocks = await _connection.Table<BlockInstance>()
            .Where(block => block.EntityRecordId == recordId)
            .ToListAsync();
        foreach (var block in blocks)
        {
            await _connection.DeleteAsync(block);
        }

        await _connection.DeleteAsync<EntityRecord>(recordId);
    }

    public async Task<EntityRecordModel?> GetRecordAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var record = await _connection.FindAsync<EntityRecord>(recordId);
        if (record is null || record.WorkspaceId != _workspaceContext.WorkspaceId)
        {
            return null;
        }

        var blocks = await _connection.Table<BlockInstance>()
            .Where(block => block.EntityRecordId == recordId)
            .OrderBy(block => block.DisplayOrder)
            .ToListAsync();
        var values = await _connection.Table<FieldValue>()
            .Where(value => value.EntityRecordId == recordId)
            .ToListAsync();
        var models = new List<BlockInstanceModel>(blocks.Count);
        foreach (var block in blocks)
        {
            var fieldDefinitions = await _connection.Table<FieldDefinition>()
                .Where(field => field.BlockDefinitionId == block.BlockDefinitionId)
                .ToListAsync();
            var fieldIds = fieldDefinitions.Select(field => field.Id).ToHashSet();
            models.Add(new BlockInstanceModel
            {
                Block = block,
                Values = values.Where(value => fieldIds.Contains(value.FieldDefinitionId)).ToList()
            });
        }

        return new EntityRecordModel { Record = record, Blocks = models };
    }

    private async Task<BlockDefinition?> GetBlockInWorkspaceAsync(Guid blockId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        var block = await _connection.FindAsync<BlockDefinition>(blockId);
        if (block is null)
        {
            return null;
        }

        var template = await _connection.FindAsync<EntityTemplate>(block.TemplateId);
        return template?.WorkspaceId == _workspaceContext.WorkspaceId ? block : null;
    }

    private async Task<FieldDefinition?> GetFieldInWorkspaceAsync(Guid fieldId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        var field = await _connection.FindAsync<FieldDefinition>(fieldId);
        if (field is null)
        {
            return null;
        }

        var block = await _connection.FindAsync<BlockDefinition>(field.BlockDefinitionId);
        return block is null || await GetBlockInWorkspaceAsync(block.Id, cancellationToken) is null ? null : field;
    }

    private async Task TouchTemplateForBlockAsync(Guid blockId)
    {
        var block = await _connection.FindAsync<BlockDefinition>(blockId);
        if (block is not null)
        {
            await TouchTemplateAsync(block.TemplateId);
        }
    }

    private async Task TouchTemplateForFieldAsync(Guid fieldId)
    {
        var field = await _connection.FindAsync<FieldDefinition>(fieldId);
        if (field is not null)
        {
            await TouchTemplateForBlockAsync(field.BlockDefinitionId);
        }
    }

    private async Task TouchTemplateAsync(Guid templateId)
    {
        var template = await _connection.FindAsync<EntityTemplate>(templateId);
        if (template is null || template.WorkspaceId != _workspaceContext.WorkspaceId)
        {
            return;
        }

        template.UpdatedAt = DateTime.UtcNow;
        await _connection.UpdateAsync(template);
    }

    private async Task NormalizeBlockOrderAsync(Guid templateId)
    {
        var blocks = await _connection.Table<BlockDefinition>()
            .Where(block => block.TemplateId == templateId)
            .OrderBy(block => block.DisplayOrder)
            .ToListAsync();
        for (var index = 0; index < blocks.Count; index++)
        {
            blocks[index].DisplayOrder = index;
            await _connection.UpdateAsync(blocks[index]);
        }
    }

    private async Task NormalizeFieldOrderAsync(Guid blockId)
    {
        var fields = await _connection.Table<FieldDefinition>()
            .Where(field => field.BlockDefinitionId == blockId)
            .OrderBy(field => field.DisplayOrder)
            .ToListAsync();
        for (var index = 0; index < fields.Count; index++)
        {
            fields[index].DisplayOrder = index;
            await _connection.UpdateAsync(fields[index]);
        }
    }

    private static void MoveItem<T>(IList<T> items, Func<T, bool> predicate, int offset)
    {
        var currentIndex = -1;
        for (var index = 0; index < items.Count; index++)
        {
            if (predicate(items[index]))
            {
                currentIndex = index;
                break;
            }
        }
        if (currentIndex < 0)
        {
            throw new InvalidOperationException("L’élément demandé n’existe pas.");
        }

        var targetIndex = Math.Clamp(currentIndex + offset, 0, items.Count - 1);
        if (currentIndex == targetIndex)
        {
            return;
        }

        var item = items[currentIndex];
        items.RemoveAt(currentIndex);
        items.Insert(targetIndex, item);
    }

    private async Task<TemplateDefinition> LoadTemplateAsync(EntityTemplate template, CancellationToken cancellationToken)
    {
        var blocks = await _connection.Table<BlockDefinition>()
            .Where(block => block.TemplateId == template.Id)
            .OrderBy(block => block.DisplayOrder)
            .ToListAsync();
        var models = new List<BlockDefinitionModel>(blocks.Count);
        foreach (var block in blocks)
        {
            var fields = await _connection.Table<FieldDefinition>()
                .Where(field => field.BlockDefinitionId == block.Id)
                .OrderBy(field => field.DisplayOrder)
                .ToListAsync();
            models.Add(new BlockDefinitionModel { Block = block, Fields = fields });
        }

        return new TemplateDefinition { Template = template, Blocks = models };
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await _workspaceContext.EnsureInitializedAsync(cancellationToken);
        await _initializationTask.WaitAsync(cancellationToken);
    }

    private async Task InitializeAsync()
    {
        await _connection.CreateTableAsync<EntityTemplate>();
        await _connection.CreateTableAsync<BlockDefinition>();
        await _connection.CreateTableAsync<FieldDefinition>();
        await _connection.CreateTableAsync<EntityRecord>();
        try
        {
            await _connection.ExecuteAsync(
                "ALTER TABLE EntityRecords ADD COLUMN ExternalEntityId TEXT NOT NULL DEFAULT ''");
        }
        catch (SQLiteException)
        {
        }
        await _connection.CreateTableAsync<BlockInstance>();
        await _connection.CreateTableAsync<FieldValue>();
    }
}
