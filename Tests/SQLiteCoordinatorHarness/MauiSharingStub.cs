namespace Microsoft.Maui.ApplicationModel;

public static class Share
{
    public static ShareService Default { get; } = new();
}

public sealed class ShareService
{
    public Task RequestAsync(ShareFileRequest request) => Task.CompletedTask;
}

public sealed class ShareFileRequest
{
    public string Title { get; set; } = string.Empty;
    public ShareFile? File { get; set; }
}

public sealed record ShareFile(string FullPath);
