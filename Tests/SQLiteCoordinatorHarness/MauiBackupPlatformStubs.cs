namespace Microsoft.Maui.ApplicationModel.DataTransfer;

public static class Share
{
    public static ShareDefault Default { get; } = new();
}

public sealed class ShareDefault
{
    public Task RequestAsync(ShareFileRequest request) => Task.CompletedTask;
}

public sealed class ShareFileRequest
{
    public string Title { get; set; } = string.Empty;
    public ShareFile File { get; set; } = new(string.Empty);
}

public sealed class ShareFile(string fullPath)
{
    public string FullPath { get; } = fullPath;
}
