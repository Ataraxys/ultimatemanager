using UltimateManager.Views;
using Microsoft.Extensions.DependencyInjection;
using UltimateManager.Services;

namespace UltimateManager
{
    public partial class AppShell : Shell
    {
        public IServiceProvider Services { get; }

        public AppShell(ClientsPage clientsPage, IServiceProvider services)
        {
            InitializeComponent();
            Services = services;
            Routing.RegisterRoute("ClientDetailsPage", typeof(ClientDetailsPage));
            Routing.RegisterRoute("ClientEditPage", typeof(ClientEditPage));
            Items.Add(new ShellContent
            {
                Title = "Clients",
                Route = "ClientsPage",
                Content = clientsPage
            });
        }
    }
}
