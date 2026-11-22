using UltimateManager.Models.DynamicForms;

namespace UltimateManager.ViewModels;

public sealed class DynamicBlockDisplay
{
    public string Name { get; init; } = string.Empty;
    public IList<DynamicFieldDisplay> Fields { get; init; } = [];
}

public sealed class DynamicFieldDisplay
{
    public string Label { get; init; } = string.Empty;
    public FieldType FieldType { get; init; }
    public string Value { get; init; } = string.Empty;
    public bool HasValue => !string.IsNullOrWhiteSpace(Value);
}
