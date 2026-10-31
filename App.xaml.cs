namespace UltimateManager
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        private readonly AppShell _appShell;

        public App(AppShell appShell)
        {
            InitializeComponent();
            Services = appShell.Services;
            _appShell = appShell;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(_appShell);
        }
    }
}