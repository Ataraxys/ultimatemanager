using SQLite;

namespace UltimateManager.Models.DynamicForms;

[Table("Workspaces")]
public sealed class Workspace
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public Guid DefaultTemplateId { get; set; }

    public DateTime CreatedAt { get; set; }
}

[Table("EntityTemplates")]
public sealed class EntityTemplate
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid WorkspaceId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = "Client";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("BlockDefinitions")]
public sealed class BlockDefinition
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid TemplateId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

[Table("FieldDefinitions")]
public sealed class FieldDefinition
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid BlockDefinitionId { get; set; }

    public string Label { get; set; } = string.Empty;
    public FieldType FieldType { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsRequired { get; set; }
    public string OptionsJson { get; set; } = "[]";
}

[Table("EntityRecords")]
public sealed class EntityRecord
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid WorkspaceId { get; set; }

    [Indexed]
    public Guid TemplateId { get; set; }

    [Indexed]
    public Guid ExternalEntityId { get; set; }

    public string EntityType { get; set; } = "Client";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[Table("BlockInstances")]
public sealed class BlockInstance
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid EntityRecordId { get; set; }

    [Indexed]
    public Guid BlockDefinitionId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

[Table("FieldValues")]
public sealed class FieldValue
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid EntityRecordId { get; set; }

    [Indexed]
    public Guid FieldDefinitionId { get; set; }

    public string ValueJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
