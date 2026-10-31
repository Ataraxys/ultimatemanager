namespace UltimateManager.Services;

public static class GoogleApiConfiguration
{
    public static string ApiKey
    {
        get
        {
#if ANDROID
            var context = Android.App.Application.Context;
            var resourceId = context.Resources?.GetIdentifier("google_maps_api_key", "string", context.PackageName);
            return resourceId is > 0 ? context.GetString(resourceId.Value) : string.Empty;
#else
            return string.Empty;
#endif
        }
    }
}
