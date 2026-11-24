namespace UltimateManager.Models.DynamicForms;

public sealed class TemplateDefinition
{
    public EntityTemplate Template { get; init; } = new();
    public IList<BlockDefinitionModel> Blocks { get; init; } = [];
}

public sealed class BlockDefinitionModel
{
    public BlockDefinition Block { get; init; } = new();
    public IList<FieldDefinition> Fields { get; init; } = [];
}

public sealed class EntityRecordModel
{
    public EntityRecord Record { get; init; } = new();
    public IList<BlockInstanceModel> Blocks { get; init; } = [];
}

public sealed class BlockInstanceModel
{
    public BlockInstance Block { get; init; } = new();
    public IList<FieldValue> Values { get; init; } = [];
}
