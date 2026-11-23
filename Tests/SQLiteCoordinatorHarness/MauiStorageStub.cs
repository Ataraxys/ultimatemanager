namespace Microsoft.Maui.Storage;

public static class FileSystem
{
    public static string AppDataDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "UltimateManager-Test-AppData");
}
