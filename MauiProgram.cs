using Microsoft.Extensions.Logging;
using UltimateManager.Services;
using UltimateManager.ViewModels;
using UltimateManager.Views;

namespace UltimateManager
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiMaps()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<IClientService, SqliteClientService>();
            builder.Services.AddSingleton<IClientBackupService, ClientBackupService>();
            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddSingleton<IMapProvider, GoogleMapProvider>();
            builder.Services.AddSingleton<IPlaceSearchProvider, GooglePlaceSearchProvider>();
            builder.Services.AddSingleton<IContactImporter, ContactImporter>();
            builder.Services.AddSingleton<AppShell>();

            builder.Services.AddTransient<ClientsPage>();
            builder.Services.AddTransient<ClientDetailsPage>();
            builder.Services.AddTransient<ClientEditPage>();
            builder.Services.AddTransient<ContactImportPage>();

            builder.Services.AddTransient<ClientsViewModel>();
            builder.Services.AddTransient<ClientDetailsViewModel>();
            builder.Services.AddTransient<ClientEditViewModel>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
