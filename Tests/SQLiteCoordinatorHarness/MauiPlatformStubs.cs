namespace Microsoft.Maui.Devices.Sensors
{
    public sealed class Location(double latitude, double longitude)
    {
        public double Latitude { get; } = latitude;
        public double Longitude { get; } = longitude;
    }
}

namespace Microsoft.Maui.ApplicationModel
{
    public static class MainThread
    {
        public static Task InvokeOnMainThreadAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }

    public static class Launcher
    {
        public static LauncherDefault Default { get; } = new();
    }

    public sealed class LauncherDefault
    {
        public Task<bool> OpenAsync(string uri) => Task.FromResult(true);
    }
}
