// File: App.xaml.cs
// Application startup - like PLC MAIN program

using System.Windows;
using GasFireMonitoringClient.Configuration;
using GasFireMonitoringClient.Services;

namespace GasFireMonitoringClient
{
    /// <summary>
    /// Application entry point
    /// </summary>
    public partial class App : Application
    {
        // Global services - like PLC function blocks
        public static AppSettings Settings { get; private set; } = new();
        public static ApiService? ApiService { get; set; }
        public static SignalRService? SignalRService { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize settings
            InitializeSettings();

            // Initialize services
            InitializeServices();
        }

        private void InitializeSettings()
        {
            // For now, use default settings
            // Later we'll load from configuration file
            Settings = new AppSettings
            {
                ServerBaseUrl = "http://localhost:5208", // Your server URL
                SignalRHubUrl = "http://localhost:5208/monitoringHub",
                DataRefreshInterval = 5,
                AlarmRefreshInterval = 2
            };
        }

        private void InitializeServices()
        {
            // Create API service
            ApiService = new ApiService(Settings);

            // Create SignalR service
            SignalRService = new SignalRService(Settings);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Cleanup services
            SignalRService?.Dispose();
            ApiService?.Dispose();

            base.OnExit(e);
        }
    }
}