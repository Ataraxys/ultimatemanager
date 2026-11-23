namespace Microsoft.Maui.Storage;

public static class FileSystem
{
    public static string AppDataDirectory => Path.Combine(Path.GetTempPath(), "UltimateManager-Test-AppData");
}
