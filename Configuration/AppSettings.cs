// File: Configuration/AppSettings.cs
// Application configuration - like PLC system parameters

namespace GasFireMonitoringClient.Configuration
{
    /// <summary>
    /// Application settings - similar to PLC configuration parameters
    /// </summary>
    public class AppSettings
    {
        // Server connection settings
        public string ServerBaseUrl { get; set; } = "http://localhost:5208";
        public string SignalRHubUrl { get; set; } = "http://localhost:5208/monitoringHub";

        // Connection timeouts (in seconds)
        public int HttpTimeoutSeconds { get; set; } = 30;
        public int SignalRTimeoutSeconds { get; set; } = 30;

        // Refresh intervals (in seconds)
        public int DataRefreshInterval { get; set; } = 5;
        public int AlarmRefreshInterval { get; set; } = 2;

        // UI settings
        public bool AutoLogin { get; set; } = false;
        public string LastUsername { get; set; } = "";

        // Application info
        public string ApplicationName { get; set; } = "Gas Fire Monitoring Client";
        public string Version { get; set; } = "1.0.0";
    }
}