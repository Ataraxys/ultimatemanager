using SQLite;
using UltimateManager.Models;
using UltimateManager.Models.DynamicForms;

namespace UltimateManager.Services;

public sealed class SqliteDynamicFormService : IDynamicFormService, IAsyncDisposable
{
    private readonly ISqliteDatabase _database;
    private readonly IWorkspaceContext _workspaceContext;
    private readonly Task _initializationTask;

    public SqliteDynamicFormService(ISqliteDatabase database, IWorkspaceContext workspaceContext)
    {
        _database = database;
        _workspaceContext = workspaceContext;
        _initializationTask = InitializeAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public async Task<IReadOnlyList<TemplateDefinition>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var templates = await _database.ReadAsync(connection => connection.Table<EntityTemplate>()
            .Where(template => template.WorkspaceId == _workspaceContext.WorkspaceId)
            .OrderBy(template => template.Name)
            .ToListAsync(), cancellationToken);
        var result = new List<TemplateDefinition>(templates.Count);
        foreach (var template in templates)
        {
            result.Add(await LoadTemplateAsync(template, cancellationToken));
        }

        return result;
    }

    public async Task<TemplateDefinition?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var template = await _database.ReadAsync(connection => connection.FindAsync<EntityTemplate>(templateId), cancellationToken);
        return template is null || template.WorkspaceId != _workspaceContext.WorkspaceId
            ? null
            : await LoadTemplateAsync(template, cancellationToken);
    }

    public async Task<EntityRecordModel?> GetRecordForExternalEntityAsync(
        Guid externalEntityId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var record = await _database.ReadAsync(connection => connection.Table<EntityRecord>()
            .Where(item => item.WorkspaceId == _workspaceContext.WorkspaceId &&
                           item.ExternalEntityId == externalEntityId)
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync(), cancellationToken);
        return record is null ? null : await GetRecordAsync(record.Id, cancellationToken);
    }

    public async Task<TemplateDefinition> CloneTemplateAsync(
        Guid sourceTemplateId,
        string name,
        CancellationToken cancellationToken = default,
        Guid? ownerClientId = null)
    {
        var source = await GetTemplateAsync(sourceTemplateId, cancellationToken)
            ?? throw new InvalidOperationException("Le template source est introuvable.");
        var clone = new EntityTemplate
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceContext.WorkspaceId,
            OwnerClientId = ownerClientId,
            Name = name.Trim(),
            EntityType = source.Template.EntityType,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _database.ExecuteTransactionAsync(connection =>
        {
            connection.Insert(clone);
            foreach (var sourceBlock in source.Blocks)
            {
                var block = new BlockDefinition
                {
                    Id = Guid.NewGuid(),
                    TemplateId = clone.Id,
                    Name = sourceBlock.Block.Name,
                    DisplayOrder = sourceBlock.Block.DisplayOrder
                };
                connection.Insert(block);
                foreach (var sourceField in sourceBlock.Fields)
                {
                    connection.Insert(new FieldDefinition
                    {
                        Id = Guid.NewGuid(),
                        BlockDefinitionId = block.Id,
                        Label = sourceField.Label,
                        FieldType = sourceField.FieldType,
                        DisplayOrder = sourceField.DisplayOrder,
                        IsRequired = sourceField.IsRequired,
                        OptionsJson = sourceField.OptionsJson
                    });
                }
            }
        }, cancellationToken);

        return await GetTemplateAsync(clone.Id, cancellationToken)
            ?? throw new InvalidOperationException("Le template cloné est introuvable.");
    }

    public async Task<TemplateDefinition> EnsureClientTemplateAsync(
        Guid externalEntityId,
        Guid sourceTemplateId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var source = sourceTemplateId == Guid.Empty
            ? null
            : await GetTemplateAsync(sourceTemplateId, cancellationToken)
                ?? throw new InvalidOperationException("Le template source est introuvable.");
        var now = DateTime.UtcNow;
        var clientTemplate = new EntityTemplate
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceContext.WorkspaceId,
            OwnerClientId = externalEntityId,
            Name = $"Fiche de {externalEntityId:N}",
            EntityType = source?.Template.EntityType ?? "Client",
            CreatedAt = now,
            UpdatedAt = now
        };
        var record = new EntityRecord
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceContext.WorkspaceId,
            TemplateId = clientTemplate.Id,
            ExternalEntityId = externalEntityId,
            EntityType = clientTemplate.EntityType,
            CreatedAt = now,
            UpdatedAt = now
        };

        EntityRecord? existingRecord = null;
        await _database.ExecuteTransactionAsync(connection =>
        {
            existingRecord = connection.Table<EntityRecord>()
                .Where(record => record.WorkspaceId == _workspaceContext.WorkspaceId &&
                                 record.ExternalEntityId == externalEntityId)
                .OrderByDescending(record => record.UpdatedAt)
                .FirstOrDefault();
            if (existingRecord is not null)
            {
                return;
            }

            connection.Insert(clientTemplate);
            var instances = new List<BlockInstance>();
            if (source is not null)
            {
                foreach (var sourceBlock in source.Blocks)
                {
                    var block = new BlockDefinition
                    {
                        Id = Guid.NewGuid(),
                        TemplateId = clientTemplate.Id,
                        Name = sourceBlock.Block.Name,
                        DisplayOrder = sourceBlock.Block.DisplayOrder
                    };
                    connection.Insert(block);
                    instances.Add(new BlockInstance
                    {
                        Id = Guid.NewGuid(),
                        EntityRecordId = record.Id,
                        BlockDefinitionId = block.Id,
                        Name = block.Name,
                        DisplayOrder = block.DisplayOrder
                    });
                    foreach (var sourceField in sourceBlock.Fields)
                    {
                        connection.Insert(new FieldDefinition
                        {
                            Id = Guid.NewGuid(),
                            BlockDefinitionId = block.Id,
                            Label = sourceField.Label,
                            FieldType = sourceField.FieldType,
                            DisplayOrder = sourceField.DisplayOrder,
                            IsRequired = sourceField.IsRequired,
                            OptionsJson = sourceField.OptionsJson
                        });
                    }
                }
            }

            connection.Insert(record);
            foreach (var instance in instances)
            {
                connection.Insert(instance);
            }
        }, cancellationToken);

        if (existingRecord is not null)
        {
            await EnsureExistingRecordTemplateAsync(existingRecord, cancellationToken);
            return await GetTemplateAsync(existingRecord.TemplateId, cancellationToken)
                ?? throw new InvalidOperationException("Le template de la fiche client n’a pas pu être restauré.");
        }

        return await GetTemplateAsync(clientTemplate.Id, cancellationToken)
            ?? throw new InvalidOperationException("Le template de la fiche client est introuvable.");
    }

    private async Task EnsureExistingRecordTemplateAsync(
        EntityRecord record,
        CancellationToken cancellationToken)
    {
        await _database.ExecuteTransactionAsync(connection =>
        {
            var currentRecord = connection.Find<EntityRecord>(record.Id);
            if (currentRecord is null || currentRecord.WorkspaceId != _workspaceContext.WorkspaceId)
            {
                throw new InvalidOperationException("La fiche client n’existe plus dans cet espace de travail.");
            }

            if (currentRecord.TemplateId == Guid.Empty)
            {
                throw new InvalidOperationException("La fiche client ne contient pas d’identifiant de template valide.");
            }

            var template = connection.Find<EntityTemplate>(currentRecord.TemplateId);
            if (template is not null)
            {
                if (template.WorkspaceId != currentRecord.WorkspaceId)
                {
                    throw new InvalidOperationException(
                        "Le template associé à cette fiche appartient à un autre espace de travail.");
                }

                return;
            }

            // Restore only the missing parent row. Reusing the original ID keeps
            // any surviving block definitions and field values associated.
            var now = DateTime.UtcNow;
            connection.Insert(new EntityTemplate
            {
                Id = currentRecord.TemplateId,
                WorkspaceId = currentRecord.WorkspaceId,
                OwnerClientId = currentRecord.ExternalEntityId == Guid.Empty
                    ? null
                    : currentRecord.ExternalEntityId,
                Name = $"Fiche de {currentRecord.ExternalEntityId:N}",
                EntityType = string.IsNullOrWhiteSpace(currentRecord.EntityType)
                    ? "Client"
                    : currentRecord.EntityType,
                CreatedAt = currentRecord.CreatedAt == default ? now : currentRecord.CreatedAt,
                UpdatedAt = now
            });
        }, cancellationToken);
    }

    public async Task<EntityRecordModel> CreateClientWithDraftAsync(
        Client client,
        TemplateDefinition draft,
        IReadOnlyDictionary<Guid, string> fieldValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(fieldValues);
        await EnsureInitializedAsync(cancellationToken);
        await EnsureClientTableAsync(cancellationToken);

        var sourceFields = draft.Blocks.SelectMany(block => block.Fields).ToList();
        if (sourceFields.Select(field => field.Id).Distinct().Count() != sourceFields.Count ||
            fieldValues.Keys.Any(fieldId => sourceFields.All(field => field.Id != fieldId)))
        {
            throw new InvalidOperationException("La structure de la fiche contient des champs invalides ou dupliqués.");
        }

        var now = DateTime.UtcNow;
        client.WorkspaceId = _workspaceContext.WorkspaceId;
        client.CreatedAt = client.CreatedAt == default ? now : client.CreatedAt;
        client.UpdatedAt = now;
        var template = new EntityTemplate
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceContext.WorkspaceId,
            OwnerClientId = client.Id,
            Name = $"Fiche de {client.Id:N}",
            EntityType = string.IsNullOrWhiteSpace(draft.Template.EntityType) ? "Client" : draft.Template.EntityType,
            CreatedAt = now,
            UpdatedAt = now
        };
        var record = new EntityRecord
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceContext.WorkspaceId,
            TemplateId = template.Id,
            ExternalEntityId = client.Id,
            EntityType = template.EntityType,
            CreatedAt = now,
            UpdatedAt = now
        };
        var clonedFields = new Dictionary<Guid, Guid>();

        await _database.ExecuteTransactionAsync(connection =>
        {
            if (connection.Find<Client>(client.Id) is not null)
            {
                throw new InvalidOperationException("Un client portant cet identifiant existe déjà.");
            }

            connection.Insert(client);
            connection.Insert(template);
            connection.Insert(record);
            foreach (var draftBlock in draft.Blocks.OrderBy(item => item.Block.DisplayOrder))
            {
                var block = new BlockDefinition
                {
                    Id = Guid.NewGuid(),
                    TemplateId = template.Id,
                    Name = draftBlock.Block.Name.Trim(),
                    DisplayOrder = draftBlock.Block.DisplayOrder
                };
                connection.Insert(block);
                connection.Insert(new BlockInstance
                {
                    Id = Guid.NewGuid(),
                    EntityRecordId = record.Id,
                    BlockDefinitionId = block.Id,
                    Name = block.Name,
                    DisplayOrder = block.DisplayOrder
                });

                foreach (var draftField in draftBlock.Fields.OrderBy(item => item.DisplayOrder))
                {
                    var field = new FieldDefinition
                    {
                        Id = Guid.NewGuid(),
                        BlockDefinitionId = block.Id,
                        Label = draftField.Label.Trim(),
                        FieldType = draftField.FieldType,
                        DisplayOrder = draftField.DisplayOrder,
                        IsRequired = draftField.IsRequired,
                        OptionsJson = draftField.OptionsJson
                    };
                    connection.Insert(field);
                    clonedFields.Add(draftField.Id, field.Id);
                }
            }

            foreach (var (draftFieldId, valueJson) in fieldValues)
            {
                connection.Insert(new FieldValue
                {
                    Id = Guid.NewGuid(),
                    EntityRecordId = record.Id,
                    FieldDefinitionId = clonedFields[draftFieldId],
                    ValueJson = valueJson,
                    UpdatedAt = now
                });
            }
        }, cancellationToken);

        return await GetRecordAsync(record.Id, cancellationToken)
            ?? throw new InvalidOperationException("La fiche client enregistrée est introuvable.");
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
        await _database.ExecuteWriteAsync(connection => connection.InsertAsync(template), cancellationToken);
        return await LoadTemplateAsync(template, cancellationToken);
    }

    public async Task<TemplateDefinition> EnsureDefaultClientTemplateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var existing = await _database.ReadAsync(connection => connection.Table<EntityTemplate>()
            .Where(template => template.WorkspaceId == _workspaceContext.WorkspaceId &&
                               template.EntityType == "Client" &&
                               template.Name == "Fiche client")
            .FirstOrDefaultAsync(), cancellationToken);
        if (existing is not null)
        {
            await SetDefaultTemplateIfEmptyAsync(existing.Id);
            return await LoadTemplateAsync(existing, cancellationToken);
        }

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

        var template = new EntityTemplate
        {
            Id = Guid.NewGuid(),
            WorkspaceId = _workspaceContext.WorkspaceId,
            Name = "Fiche client",
            EntityType = "Client",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _database.ExecuteTransactionAsync(connection =>
        {
            connection.Insert(template);
            var blockOrder = 0;
            foreach (var blockDefinition in blocks)
            {
                var block = new BlockDefinition
                {
                    Id = Guid.NewGuid(),
                    TemplateId = template.Id,
                    Name = blockDefinition.Name,
                    DisplayOrder = blockOrder++
                };
                connection.Insert(block);
                var fieldOrder = 0;
                foreach (var fieldDefinition in blockDefinition.Fields)
                {
                    connection.Insert(new FieldDefinition
                    {
                        Id = Guid.NewGuid(),
                        BlockDefinitionId = block.Id,
                        Label = fieldDefinition.Item1,
                        FieldType = fieldDefinition.Item2,
                        DisplayOrder = fieldOrder++
                    });
                }
            }
            var workspace = connection.Find<Workspace>(_workspaceContext.WorkspaceId)
                ?? throw new InvalidOperationException("L’espace de travail est introuvable.");
            if (workspace.DefaultTemplateId == Guid.Empty)
            {
                workspace.DefaultTemplateId = template.Id;
                connection.Update(workspace);
            }
        }, cancellationToken);
        return await LoadTemplateAsync(template, cancellationToken);
    }

    public async Task<TemplateDefinition> GetDefaultClientTemplateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var workspace = await _database.ReadAsync(connection => connection.FindAsync<Workspace>(_workspaceContext.WorkspaceId), cancellationToken);
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

        var workspace = await _database.ReadAsync(connection => connection.FindAsync<Workspace>(_workspaceContext.WorkspaceId), cancellationToken)
            ?? throw new InvalidOperationException("L’espace de travail est introuvable.");
        workspace.DefaultTemplateId = templateId;
        await _database.ExecuteWriteAsync(connection => connection.UpdateAsync(workspace), cancellationToken);
    }

    private async Task SetDefaultTemplateIfEmptyAsync(Guid templateId)
    {
        var workspace = await _database.ReadAsync(connection => connection.FindAsync<Workspace>(_workspaceContext.WorkspaceId));
        if (workspace is null || workspace.DefaultTemplateId != Guid.Empty)
        {
            return;
        }

        workspace.DefaultTemplateId = templateId;
        await _database.ExecuteWriteAsync(connection => connection.UpdateAsync(workspace));
    }

    public async Task DeleteTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _database.ExecuteTransactionAsync(connection =>
        {
            var template = connection.Find<EntityTemplate>(templateId);
            if (template is null || template.WorkspaceId != _workspaceContext.WorkspaceId)
            {
                throw new InvalidOperationException("Le modèle demandé n’existe pas dans cet espace de travail.");
            }

            var workspace = connection.Find<Workspace>(_workspaceContext.WorkspaceId);
            if (workspace?.DefaultTemplateId == templateId)
            {
                throw new InvalidOperationException("Le modèle par défaut ne peut pas être supprimé.");
            }

            if (connection.Table<EntityRecord>().Any(record => record.TemplateId == templateId))
            {
                throw new InvalidOperationException("Ce modèle est utilisé par une fiche client et ne peut pas être supprimé.");
            }

            DeleteTemplateDefinitions(connection, templateId);
            connection.Delete(template);
        }, cancellationToken);
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
            DisplayOrder = 0
        };
        await _database.ExecuteTransactionAsync(connection =>
        {
            block.DisplayOrder = connection.Table<BlockDefinition>()
                .Where(item => item.TemplateId == template.Template.Id)
                .Count();
            connection.Insert(block);
            TouchTemplate(connection, template.Template.Id);
        }, cancellationToken);
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
        var field = new FieldDefinition
        {
            Id = Guid.NewGuid(),
            BlockDefinitionId = block.Id,
            Label = label.Trim(),
            FieldType = fieldType,
            DisplayOrder = 0
        };
        await _database.ExecuteTransactionAsync(connection =>
        {
            field.DisplayOrder = connection.Table<FieldDefinition>()
                .Where(item => item.BlockDefinitionId == block.Id)
                .Count();
            connection.Insert(field);
            TouchTemplateForBlock(connection, block.Id);
        }, cancellationToken);
        return field;
    }

    public async Task UpdateBlockAsync(BlockDefinition block, CancellationToken cancellationToken = default)
    {
        await _database.ExecuteTransactionAsync(connection =>
        {
            var existing = GetBlockInWorkspace(connection, block.Id, _workspaceContext.WorkspaceId)
                ?? throw new InvalidOperationException("Le bloc demandé n’existe pas dans cet espace de travail.");
            existing.Name = block.Name.Trim();
            existing.DisplayOrder = block.DisplayOrder;
            connection.Update(existing);
            TouchTemplateForBlock(connection, existing.Id);
        }, cancellationToken);
    }

    public async Task UpdateFieldAsync(FieldDefinition field, CancellationToken cancellationToken = default)
    {
        await _database.ExecuteTransactionAsync(connection =>
        {
            var existing = GetFieldInWorkspace(connection, field.Id, _workspaceContext.WorkspaceId)
                ?? throw new InvalidOperationException("Le champ demandé n’existe pas dans cet espace de travail.");
            existing.Label = field.Label.Trim();
            existing.FieldType = field.FieldType;
            existing.IsRequired = field.IsRequired;
            existing.OptionsJson = field.OptionsJson;
            existing.DisplayOrder = field.DisplayOrder;
            connection.Update(existing);
            TouchTemplateForField(connection, existing.Id);
        }, cancellationToken);
    }

    public async Task DeleteBlockAsync(Guid blockId, CancellationToken cancellationToken = default)
    {
        await _database.ExecuteTransactionAsync(connection =>
        {
            var block = GetBlockInWorkspace(connection, blockId, _workspaceContext.WorkspaceId)
                ?? throw new InvalidOperationException("Le bloc demandé n’existe pas dans cet espace de travail.");
            EnsureTemplateHasAtMostOneRecord(connection, block.TemplateId);
            DeleteFieldValuesForBlock(connection, block);
            connection.Execute("DELETE FROM BlockInstances WHERE BlockDefinitionId = ?", block.Id);
            connection.Execute("DELETE FROM FieldDefinitions WHERE BlockDefinitionId = ?", block.Id);
            connection.Delete(block);
            NormalizeBlockOrder(connection, block.TemplateId);
            TouchTemplate(connection, block.TemplateId);
        }, cancellationToken);
    }

    public async Task DeleteFieldAsync(Guid fieldId, CancellationToken cancellationToken = default)
    {
        await _database.ExecuteTransactionAsync(connection =>
        {
            var field = GetFieldInWorkspace(connection, fieldId, _workspaceContext.WorkspaceId)
                ?? throw new InvalidOperationException("Le champ demandé n’existe pas dans cet espace de travail.");
            var block = connection.Find<BlockDefinition>(field.BlockDefinitionId)!;
            EnsureTemplateHasAtMostOneRecord(connection, block.TemplateId);
            DeleteFieldValuesForField(connection, block.TemplateId, field.Id);
            connection.Delete(field);
            NormalizeFieldOrder(connection, field.BlockDefinitionId);
            TouchTemplate(connection, block.TemplateId);
        }, cancellationToken);
    }

    public async Task MoveBlockAsync(Guid templateId, Guid blockId, int offset, CancellationToken cancellationToken = default)
    {
        await _database.ExecuteTransactionAsync(connection =>
        {
            var template = connection.Find<EntityTemplate>(templateId);
            if (template is null || template.WorkspaceId != _workspaceContext.WorkspaceId)
            {
                throw new InvalidOperationException("Le modèle demandé n’existe pas dans cet espace de travail.");
            }

            var blocks = connection.Table<BlockDefinition>()
                .Where(block => block.TemplateId == templateId)
                .OrderBy(block => block.DisplayOrder)
                .ToList();
            MoveItem<BlockDefinition>(blocks, block => block.Id == blockId, offset);
            for (var index = 0; index < blocks.Count; index++)
            {
                blocks[index].DisplayOrder = index;
                connection.Update(blocks[index]);
            }
            TouchTemplate(connection, templateId);
        }, cancellationToken);
    }

    public async Task MoveFieldAsync(Guid blockId, Guid fieldId, int offset, CancellationToken cancellationToken = default)
    {
        await _database.ExecuteTransactionAsync(connection =>
        {
            var block = GetBlockInWorkspace(connection, blockId, _workspaceContext.WorkspaceId)
                ?? throw new InvalidOperationException("Le bloc demandé n’existe pas dans cet espace de travail.");
            var fields = connection.Table<FieldDefinition>()
                .Where(field => field.BlockDefinitionId == block.Id)
                .OrderBy(field => field.DisplayOrder)
                .ToList();
            MoveItem<FieldDefinition>(fields, field => field.Id == fieldId, offset);
            for (var index = 0; index < fields.Count; index++)
            {
                fields[index].DisplayOrder = index;
                connection.Update(fields[index]);
            }
            TouchTemplate(connection, block.TemplateId);
        }, cancellationToken);
    }

    public async Task SaveFieldValueAsync(
        Guid recordId,
        Guid fieldDefinitionId,
        string valueJson,
        CancellationToken cancellationToken = default)
    {
        await SaveFieldValuesAsync(recordId, new Dictionary<Guid, string> { [fieldDefinitionId] = valueJson }, cancellationToken);
    }

    public async Task SaveFieldValuesAsync(
        Guid recordId,
        IReadOnlyDictionary<Guid, string> values,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (values.Count == 0)
        {
            return;
        }

        await _database.ExecuteTransactionAsync(connection =>
        {
            var record = connection.Find<EntityRecord>(recordId);
            if (record is null || record.WorkspaceId != _workspaceContext.WorkspaceId)
            {
                throw new InvalidOperationException("La fiche demandée n’existe pas dans cet espace de travail.");
            }

            foreach (var (fieldDefinitionId, valueJson) in values)
            {
                var field = connection.Find<FieldDefinition>(fieldDefinitionId);
                var block = field is null ? null : connection.Find<BlockDefinition>(field.BlockDefinitionId);
                var template = block is null ? null : connection.Find<EntityTemplate>(block.TemplateId);
                if (template is null || template.WorkspaceId != _workspaceContext.WorkspaceId || template.Id != record.TemplateId)
                {
                    throw new InvalidOperationException("La fiche ou le champ demandé n’existe pas dans cet espace de travail.");
                }

                var value = connection.Table<FieldValue>()
                    .Where(item => item.EntityRecordId == recordId && item.FieldDefinitionId == fieldDefinitionId)
                    .FirstOrDefault();
                if (value is null)
                {
                    connection.Insert(new FieldValue
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
                    connection.Update(value);
                }
            }

            record.UpdatedAt = DateTime.UtcNow;
            connection.Update(record);
        });
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
            blocks.Add(new BlockInstanceModel { Block = block, Values = [] });
        }

        await _database.ExecuteTransactionAsync(connection =>
        {
            connection.Insert(record);
            foreach (var block in blocks)
            {
                connection.Insert(block.Block);
            }
        }, cancellationToken);

        return new EntityRecordModel { Record = record, Blocks = blocks };
    }

    public async Task<EntityRecordModel> GetOrCreateRecordAsync(
        Guid externalEntityId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var recordId = Guid.Empty;
        await _database.ExecuteTransactionAsync(connection =>
        {
            var existing = connection.Table<EntityRecord>()
                .Where(item => item.WorkspaceId == _workspaceContext.WorkspaceId && item.ExternalEntityId == externalEntityId)
                .OrderByDescending(item => item.UpdatedAt)
                .FirstOrDefault();
            if (existing is not null)
            {
                if (existing.TemplateId != templateId)
                {
                    throw new InvalidOperationException(
                        "Cette fiche possède déjà des données associées à un autre modèle. " +
                        "Le changement de modèle est bloqué tant qu'une migration qui conserve ces données n'est pas disponible.");
                }

                recordId = existing.Id;
                return;
            }

            var template = connection.Find<EntityTemplate>(templateId);
            if (template is null || template.WorkspaceId != _workspaceContext.WorkspaceId)
            {
                throw new InvalidOperationException("Le modèle demandé n’existe pas dans cet espace de travail.");
            }

            var now = DateTime.UtcNow;
            var record = new EntityRecord
            {
                Id = Guid.NewGuid(),
                WorkspaceId = _workspaceContext.WorkspaceId,
                TemplateId = templateId,
                ExternalEntityId = externalEntityId,
                EntityType = template.EntityType,
                CreatedAt = now,
                UpdatedAt = now
            };
            recordId = record.Id;
            connection.Insert(record);
            foreach (var definition in connection.Table<BlockDefinition>()
                         .Where(item => item.TemplateId == templateId)
                         .OrderBy(item => item.DisplayOrder)
                         .ToList())
            {
                connection.Insert(new BlockInstance
                {
                    Id = Guid.NewGuid(),
                    EntityRecordId = record.Id,
                    BlockDefinitionId = definition.Id,
                    Name = definition.Name,
                    DisplayOrder = definition.DisplayOrder
                });
            }
        }, cancellationToken);

        return await GetRecordAsync(recordId == Guid.Empty
                ? throw new InvalidOperationException("La création de la fiche a échoué.")
                : recordId,
                cancellationToken)
            ?? throw new InvalidOperationException("La fiche dynamique existante est inaccessible.");
    }

    // Aucune suppression ni écrasement : seules les instances absentes sont créées.
    private async Task EnsureBlockInstancesAsync(EntityRecord record)
    {
        await _database.ExecuteTransactionAsync(connection =>
        {
            var definitions = connection.Table<BlockDefinition>()
                .Where(definition => definition.TemplateId == record.TemplateId)
                .OrderBy(definition => definition.DisplayOrder)
                .ToList();
            var existingIds = connection.Table<BlockInstance>()
                .Where(block => block.EntityRecordId == record.Id)
                .ToList()
                .Select(block => block.BlockDefinitionId)
                .ToHashSet();
            foreach (var definition in definitions.Where(definition => !existingIds.Contains(definition.Id)))
            {
                connection.Insert(new BlockInstance
                {
                    Id = Guid.NewGuid(),
                    EntityRecordId = record.Id,
                    BlockDefinitionId = definition.Id,
                    Name = definition.Name,
                    DisplayOrder = definition.DisplayOrder
                });
            }
        });
    }

    public async Task<EntityRecordModel?> GetRecordAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        var record = await _database.ReadAsync(connection => connection.FindAsync<EntityRecord>(recordId), cancellationToken);
        if (record is null || record.WorkspaceId != _workspaceContext.WorkspaceId)
        {
            return null;
        }

        await EnsureBlockInstancesAsync(record);
        return await _database.ReadAsync(async connection =>
        {
            var currentRecord = await connection.FindAsync<EntityRecord>(recordId);
            if (currentRecord is null || currentRecord.WorkspaceId != _workspaceContext.WorkspaceId)
            {
                return null;
            }

            var allBlocks = await connection.Table<BlockInstance>()
                .Where(block => block.EntityRecordId == recordId)
                .OrderBy(block => block.DisplayOrder)
                .ToListAsync();
            var definitions = await connection.Table<BlockDefinition>()
                .Where(definition => definition.TemplateId == currentRecord.TemplateId)
                .ToListAsync();
            var definitionIds = definitions.Select(definition => definition.Id).ToHashSet();
            var blocks = allBlocks
                .Where(block => definitionIds.Contains(block.BlockDefinitionId))
                .GroupBy(block => block.BlockDefinitionId)
                .Select(group => group.OrderBy(block => block.Id).First())
                .OrderBy(block => block.DisplayOrder)
                .ToList();
            var values = await connection.Table<FieldValue>()
                .Where(value => value.EntityRecordId == recordId)
                .ToListAsync();
            var models = new List<BlockInstanceModel>(blocks.Count);
            foreach (var block in blocks)
            {
                var fieldDefinitions = await connection.Table<FieldDefinition>()
                    .Where(field => field.BlockDefinitionId == block.BlockDefinitionId)
                    .ToListAsync();
                var fieldIds = fieldDefinitions.Select(field => field.Id).ToHashSet();
                models.Add(new BlockInstanceModel
                {
                    Block = block,
                    Values = values.Where(value => fieldIds.Contains(value.FieldDefinitionId)).ToList()
                });
            }

            return new EntityRecordModel { Record = currentRecord, Blocks = models };
        }, cancellationToken);
    }

    private async Task<BlockDefinition?> GetBlockInWorkspaceAsync(Guid blockId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _database.ReadAsync(async connection =>
        {
            var block = await connection.FindAsync<BlockDefinition>(blockId);
            if (block is null)
            {
                return null;
            }

            var template = await connection.FindAsync<EntityTemplate>(block.TemplateId);
            return template?.WorkspaceId == _workspaceContext.WorkspaceId ? block : null;
        }, cancellationToken);
    }

    private async Task<FieldDefinition?> GetFieldInWorkspaceAsync(Guid fieldId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _database.ReadAsync(async connection =>
        {
            var field = await connection.FindAsync<FieldDefinition>(fieldId);
            if (field is null)
            {
                return null;
            }

            var block = await connection.FindAsync<BlockDefinition>(field.BlockDefinitionId);
            var template = block is null ? null : await connection.FindAsync<EntityTemplate>(block.TemplateId);
            return template?.WorkspaceId == _workspaceContext.WorkspaceId ? field : null;
        }, cancellationToken);
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
        return await _database.ReadAsync(async connection =>
        {
            var current = await connection.FindAsync<EntityTemplate>(template.Id);
            if (current is null || current.WorkspaceId != _workspaceContext.WorkspaceId)
            {
                return null;
            }

            var blocks = await connection.Table<BlockDefinition>()
                .Where(block => block.TemplateId == current.Id)
                .OrderBy(block => block.DisplayOrder)
                .ToListAsync();
            var models = new List<BlockDefinitionModel>(blocks.Count);
            foreach (var block in blocks)
            {
                var fields = await connection.Table<FieldDefinition>()
                    .Where(field => field.BlockDefinitionId == block.Id)
                    .OrderBy(field => field.DisplayOrder)
                    .ToListAsync();
                models.Add(new BlockDefinitionModel { Block = block, Fields = fields });
            }

            return new TemplateDefinition { Template = current, Blocks = models };
        }, cancellationToken) ?? throw new InvalidOperationException("Le modèle demandé n’existe pas dans cet espace de travail.");
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await _workspaceContext.EnsureInitializedAsync(cancellationToken);
        await _initializationTask.WaitAsync(cancellationToken);
    }

    private async Task EnsureClientTableAsync(CancellationToken cancellationToken)
    {
        var exists = await _database.ReadAsync(async connection =>
            await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Clients'") == 1,
            cancellationToken);
        if (exists)
        {
            var columns = await _database.ReadAsync(connection => connection.GetTableInfoAsync("Clients"), cancellationToken);
            if (columns.All(column => column.Name != nameof(Client.WorkspaceId)))
            {
                await _database.ExecuteWriteAsync(connection => connection.ExecuteAsync(
                    "ALTER TABLE Clients ADD COLUMN WorkspaceId TEXT NOT NULL DEFAULT ''"), cancellationToken);
            }
        }

        await _database.ExecuteWriteAsync(connection => connection.CreateTableAsync<Client>(), cancellationToken);
        await _database.ExecuteWriteAsync(connection => connection.ExecuteAsync(
            "UPDATE Clients SET WorkspaceId = ? WHERE WorkspaceId IS NULL OR WorkspaceId = '' OR WorkspaceId = ?",
            _workspaceContext.WorkspaceId,
            Guid.Empty), cancellationToken);
    }

    private async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var recordsTableExists = await _database.ReadAsync(async connection =>
            await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'EntityRecords'") == 1);
        if (recordsTableExists)
        {
            var existingRecordColumns = await _database.ReadAsync(connection => connection.GetTableInfoAsync("EntityRecords"));
            if (existingRecordColumns.All(column => column.Name != nameof(EntityRecord.ExternalEntityId)))
            {
                await _database.ExecuteWriteAsync(connection => connection.ExecuteAsync(
                    "ALTER TABLE EntityRecords ADD COLUMN ExternalEntityId TEXT NOT NULL DEFAULT ''"));
            }
        }

        await _database.ExecuteWriteAsync(async connection =>
        {
            await connection.CreateTableAsync<EntityTemplate>();
            await connection.CreateTableAsync<BlockDefinition>();
            await connection.CreateTableAsync<FieldDefinition>();
            await connection.CreateTableAsync<EntityRecord>();
            await connection.CreateTableAsync<BlockInstance>();
            await connection.CreateTableAsync<FieldValue>();
        });

        var templateColumns = await _database.ReadAsync(connection => connection.GetTableInfoAsync("EntityTemplates"));
        if (templateColumns.All(column => column.Name != nameof(EntityTemplate.OwnerClientId)))
        {
            await _database.ExecuteWriteAsync(connection => connection.ExecuteAsync(
                "ALTER TABLE EntityTemplates ADD COLUMN OwnerClientId TEXT NULL"));
        }

        await _database.ExecuteTransactionAsync(connection =>
        {
            var records = connection.Table<EntityRecord>()
                .Where(record => record.ExternalEntityId != Guid.Empty)
                .ToList();
            foreach (var record in records)
            {
                var template = connection.Find<EntityTemplate>(record.TemplateId);
                if (template is null || template.WorkspaceId != record.WorkspaceId || template.OwnerClientId is not null ||
                    template.Name != $"Fiche de {record.ExternalEntityId:N}")
                {
                    continue;
                }

                template.OwnerClientId = record.ExternalEntityId;
                connection.Update(template);
            }
        });
    }

    private static BlockDefinition? GetBlockInWorkspace(
        SQLiteConnection connection,
        Guid blockId,
        Guid workspaceId)
    {
        var block = connection.Find<BlockDefinition>(blockId);
        var template = block is null ? null : connection.Find<EntityTemplate>(block.TemplateId);
        return template?.WorkspaceId == workspaceId ? block : null;
    }

    private static FieldDefinition? GetFieldInWorkspace(
        SQLiteConnection connection,
        Guid fieldId,
        Guid workspaceId)
    {
        var field = connection.Find<FieldDefinition>(fieldId);
        return field is not null && GetBlockInWorkspace(connection, field.BlockDefinitionId, workspaceId) is not null
            ? field
            : null;
    }

    private void TouchTemplate(SQLiteConnection connection, Guid templateId)
    {
        var template = connection.Find<EntityTemplate>(templateId);
        if (template is null || template.WorkspaceId != _workspaceContext.WorkspaceId)
        {
            return;
        }

        template.UpdatedAt = DateTime.UtcNow;
        connection.Update(template);
    }

    private void TouchTemplateForBlock(SQLiteConnection connection, Guid blockId)
    {
        var block = connection.Find<BlockDefinition>(blockId);
        if (block is not null)
        {
            TouchTemplate(connection, block.TemplateId);
        }
    }

    private void TouchTemplateForField(SQLiteConnection connection, Guid fieldId)
    {
        var field = connection.Find<FieldDefinition>(fieldId);
        if (field is not null)
        {
            TouchTemplateForBlock(connection, field.BlockDefinitionId);
        }
    }

    private void EnsureTemplateHasAtMostOneRecord(SQLiteConnection connection, Guid templateId)
    {
        var records = connection.Table<EntityRecord>()
            .Where(record => record.TemplateId == templateId)
            .ToList();
        if (records.Count > 1 || records.Any(record => record.WorkspaceId != _workspaceContext.WorkspaceId))
        {
            throw new InvalidOperationException(
                "La structure est utilisée par plusieurs fiches. Clonez le template avant de supprimer des blocs ou champs.");
        }
    }

    private static void DeleteFieldValuesForBlock(SQLiteConnection connection, BlockDefinition block)
    {
        connection.Execute(
            "DELETE FROM FieldValues WHERE EntityRecordId IN (SELECT Id FROM EntityRecords WHERE TemplateId = ?) " +
            "AND FieldDefinitionId IN (SELECT Id FROM FieldDefinitions WHERE BlockDefinitionId = ?)",
            block.TemplateId,
            block.Id);
    }

    private static void DeleteFieldValuesForField(SQLiteConnection connection, Guid templateId, Guid fieldId)
    {
        connection.Execute(
            "DELETE FROM FieldValues WHERE FieldDefinitionId = ? " +
            "AND EntityRecordId IN (SELECT Id FROM EntityRecords WHERE TemplateId = ?)",
            fieldId,
            templateId);
    }

    private static void DeleteTemplateDefinitions(SQLiteConnection connection, Guid templateId)
    {
        var blocks = connection.Table<BlockDefinition>()
            .Where(block => block.TemplateId == templateId)
            .ToList();
        foreach (var block in blocks)
        {
            DeleteFieldValuesForBlock(connection, block);
            connection.Execute("DELETE FROM BlockInstances WHERE BlockDefinitionId = ?", block.Id);
            connection.Execute("DELETE FROM FieldDefinitions WHERE BlockDefinitionId = ?", block.Id);
            connection.Delete(block);
        }
    }

    private static void NormalizeBlockOrder(SQLiteConnection connection, Guid templateId)
    {
        var blocks = connection.Table<BlockDefinition>()
            .Where(block => block.TemplateId == templateId)
            .OrderBy(block => block.DisplayOrder)
            .ToList();
        for (var index = 0; index < blocks.Count; index++)
        {
            blocks[index].DisplayOrder = index;
            connection.Update(blocks[index]);
        }
    }

    private static void NormalizeFieldOrder(SQLiteConnection connection, Guid blockId)
    {
        var fields = connection.Table<FieldDefinition>()
            .Where(field => field.BlockDefinitionId == blockId)
            .OrderBy(field => field.DisplayOrder)
            .ToList();
        for (var index = 0; index < fields.Count; index++)
        {
            fields[index].DisplayOrder = index;
            connection.Update(fields[index]);
        }
    }
}
