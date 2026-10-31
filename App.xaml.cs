namespace UltimateManager
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        private readonly AppShell _appShell;

        public App(IServiceProvider services)
        {
            InitializeComponent();
            Services = services;
            _appShell = services.GetRequiredService<AppShell>();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(_appShell);
        }
    }
}