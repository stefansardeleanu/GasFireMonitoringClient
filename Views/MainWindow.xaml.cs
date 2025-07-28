// File: Views/MainWindow.xaml.cs
// Complete main window code-behind with all functionality

using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using GasFireMonitoringClient.Services;
using GasFireMonitoringClient.Models.Entities;
using GasFireMonitoringClient.Models.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Controls;

namespace GasFireMonitoringClient.Views
{
    /// <summary>
    /// Main window with complete functionality
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Private Fields
        private ApiService? _apiService;
        private SignalRService? _signalRService;
        private DispatcherTimer? _autoRefreshTimer;
        private bool _autoRefreshEnabled = false;

        // View models for data binding
        private ObservableCollection<SiteViewModel> _sitesCollection = new();
        private ObservableCollection<SensorViewModel> _sensorsCollection = new();
        private ObservableCollection<AlarmViewModel> _alarmsCollection = new();

        // Current selected site for sensor filtering
        private int _selectedSiteId = -1;
        #endregion

        #region Constructor and Initialization
        public MainWindow()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            _apiService = App.ApiService;
            _signalRService = App.SignalRService;

            SetupDataGrids();
            SetupAutoRefreshTimer();
            SetupSignalREventHandlers();
            UpdateConnectionStatus();
            UpdateServerUrl();
            LogMessage("Application started. Click 'Test Connection' to verify server connectivity.");
        }

        private void SetupDataGrids()
        {
            // Bind data grids to collections
            SitesDataGrid.ItemsSource = _sitesCollection;
            SensorsDataGrid.ItemsSource = _sensorsCollection;
            AlarmsDataGrid.ItemsSource = _alarmsCollection;

            // Setup site filter combo box
            SiteFilterComboBox.ItemsSource = _sitesCollection;
            SiteFilterComboBox.DisplayMemberPath = "Name";
            SiteFilterComboBox.SelectedValuePath = "Id";

            // Setup map navigation
            SetupMapNavigation();
        }

        private void SetupAutoRefreshTimer()
        {
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(App.Settings.DataRefreshInterval)
            };
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
        }
        #endregion

        #region UI Update Methods
        private void UpdateConnectionStatus()
        {
            bool isConnected = _apiService?.IsLoggedIn == true;

            // Update connection status display
            ConnectionStatus.Text = isConnected ? "Connected" : "Disconnected";
            ConnectionStatus.Foreground = isConnected ? Brushes.Green : Brushes.Red;

            // Update user info
            UserInfo.Text = isConnected
                ? $"User: {_apiService.CurrentUser?.Username} ({_apiService.CurrentUser?.Role})"
                : "";

            // Enable/disable tabs based on connection
            SetTabsEnabled(isConnected);
        }

        private void SetTabsEnabled(bool enabled)
        {
            foreach (TabItem tab in MainTabs.Items)
            {
                var header = tab.Header.ToString();
                if (header != "Connection Test" && header != "Real-time Monitor")
                    tab.IsEnabled = enabled;
            }
        }

        private void UpdateServerUrl()
        {
            ServerUrlTextBox.Text = App.Settings.ServerBaseUrl;
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}\n";

            ResultsTextBox.AppendText(logEntry);
            ResultsTextBox.ScrollToEnd();

            UpdateStatusBar(message, timestamp);
        }

        private void UpdateStatusBar(string message, string timestamp)
        {
            StatusText.Text = message;
            LastUpdateText.Text = $"Last update: {timestamp}";
        }
        #endregion

        #region Enhanced UI Data Management
        private async Task RefreshSitesData()
        {
            try
            {
                var sites = await _apiService!.GetSitesAsync();

                // Update sites collection
                _sitesCollection.Clear();
                foreach (var site in sites)
                {
                    var siteViewModel = new SiteViewModel();
                    siteViewModel.UpdateFromSite(site);
                    _sitesCollection.Add(siteViewModel);
                }

                // Update dashboard summary
                UpdateDashboardSummary();

                // Update map views
                UpdateMapViews();

                SitesLastUpdateText.Text = DateTime.Now.ToString("HH:mm:ss");
                LogMessage($"✅ Loaded {sites.Count} sites");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error loading sites: {ex.Message}");
            }
        }

        private async Task RefreshSensorsData(int? siteId = null)
        {
            try
            {
                List<Sensor> sensors;

                if (siteId.HasValue)
                {
                    sensors = await _apiService!.GetSensorsAsync(siteId.Value);
                }
                else
                {
                    // Get sensors for all sites
                    sensors = new List<Sensor>();
                    foreach (var site in _sitesCollection)
                    {
                        var siteSensors = await _apiService!.GetSensorsAsync(site.Id);
                        sensors.AddRange(siteSensors);
                    }
                }

                // Update sensors collection
                _sensorsCollection.Clear();
                foreach (var sensor in sensors)
                {
                    var sensorViewModel = new SensorViewModel();
                    sensorViewModel.UpdateFromSensor(sensor);
                    _sensorsCollection.Add(sensorViewModel);
                }

                SensorCountText.Text = sensors.Count.ToString();
                LogMessage($"✅ Loaded {sensors.Count} sensors");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error loading sensors: {ex.Message}");
            }
        }

        private async Task RefreshAlarmsData()
        {
            try
            {
                var alarms = await _apiService!.GetAlarmsAsync(limit: 100);

                // Update alarms collection
                _alarmsCollection.Clear();
                foreach (var alarm in alarms)
                {
                    var alarmViewModel = new AlarmViewModel();
                    alarmViewModel.UpdateFromAlarm(alarm);
                    _alarmsCollection.Add(alarmViewModel);
                }

                UpdateAlarmCounts();
                LogMessage($"✅ Loaded {alarms.Count} alarms");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error loading alarms: {ex.Message}");
            }
        }

        private void UpdateDashboardSummary()
        {
            TotalSitesText.Text = _sitesCollection.Count.ToString();
            ActiveSitesText.Text = _sitesCollection.Count(s => s.Status == "normal").ToString();
            TotalSensorsText.Text = _sitesCollection.Sum(s => s.TotalSensors).ToString();
            ActiveAlarmsText.Text = _sitesCollection.Sum(s => s.AlarmSensors).ToString();
        }

        private void UpdateAlarmCounts()
        {
            AlarmCountText.Text = _alarmsCollection.Count.ToString();
            NewAlarmCountText.Text = _alarmsCollection.Count(a => a.IsNew).ToString();
        }

        private void AutoRefreshTimer_Tick(object? sender, EventArgs e)
        {
            _ = Task.Run(async () =>
            {
                await RefreshSitesData();
                if (_selectedSiteId > 0)
                {
                    await RefreshSensorsData(_selectedSiteId);
                }
            });
        }
        #endregion

        #region UI Event Handlers
        private async void RefreshSites_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteWithButtonDisabled(RefreshSitesButton, async () =>
            {
                await RefreshSitesData();
            });
        }

        private async void RefreshSensors_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteWithButtonDisabled(RefreshSensorsButton, async () =>
            {
                await RefreshSensorsData(_selectedSiteId > 0 ? _selectedSiteId : null);
            });
        }

        private async void RefreshAlarms_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteWithButtonDisabled(RefreshAlarmsButton, async () =>
            {
                await RefreshAlarmsData();
            });
        }

        private void ToggleAutoRefresh_Click(object sender, RoutedEventArgs e)
        {
            _autoRefreshEnabled = !_autoRefreshEnabled;

            if (_autoRefreshEnabled)
            {
                _autoRefreshTimer?.Start();
                AutoRefreshButton.Content = "⏱️ Auto Refresh: ON";
                LogMessage("✅ Auto-refresh enabled");
            }
            else
            {
                _autoRefreshTimer?.Stop();
                AutoRefreshButton.Content = "⏱️ Auto Refresh: OFF";
                LogMessage("🔄 Auto-refresh disabled");
            }
        }

        private async void SitesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SitesDataGrid.SelectedItem is SiteViewModel selectedSite)
            {
                _selectedSiteId = selectedSite.Id;
                SiteFilterComboBox.SelectedValue = selectedSite.Id;

                // Load sensors for selected site
                await RefreshSensorsData(selectedSite.Id);
            }
        }

        private async void SiteFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SiteFilterComboBox.SelectedValue is int siteId)
            {
                _selectedSiteId = siteId;
                await RefreshSensorsData(siteId);
            }
        }

        private void ClearNewAlarms_Click(object sender, RoutedEventArgs e)
        {
            foreach (var alarm in _alarmsCollection)
            {
                alarm.IsNew = false;
            }
            UpdateAlarmCounts();
            LogMessage("✅ Cleared new alarm flags");
        }
        #endregion

        #region Connection Management
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteWithButtonDisabled(TestConnectionButton, async () =>
            {
                LogMessage("Testing server connection...");

                UpdateServerSettings();
                await TestServerConnection();
            });
        }

        private void UpdateServerSettings()
        {
            App.Settings.ServerBaseUrl = ServerUrlTextBox.Text.TrimEnd('/');

            // Recreate API service with updated URL
            _apiService?.Dispose();
            _apiService = new ApiService(App.Settings);
            App.ApiService = _apiService;
        }

        private async Task TestServerConnection()
        {
            try
            {
                var isConnected = await _apiService!.TestConnectionAsync();

                if (isConnected)
                {
                    LogMessage("✅ Server connection successful!");
                    LogMessage($"Connected to: {App.Settings.ServerBaseUrl}");
                }
                else
                {
                    LogMessage("❌ Server connection failed!");
                    LogMessage("Please check the server URL and ensure the server is running.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Connection error: {ex.Message}");
            }
        }
        #endregion

        #region Authentication
        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteWithButtonDisabled(LoginButton, async () =>
            {
                LogMessage("Attempting login...");

                if (!ValidateLoginInputs())
                    return;

                await PerformLogin();
            });
        }

        private bool ValidateLoginInputs()
        {
            if (_apiService == null)
            {
                LogMessage("❌ Please test connection first!");
                return false;
            }

            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                LogMessage("❌ Please enter username and password!");
                return false;
            }

            return true;
        }

        private async Task PerformLogin()
        {
            try
            {
                var username = UsernameTextBox.Text.Trim();
                var password = PasswordBox.Password;

                var (success, message) = await _apiService!.LoginAsync(username, password);

                if (success)
                {
                    HandleSuccessfulLogin(username);
                    await LoadInitialData();
                }
                else
                {
                    LogMessage($"❌ Login failed: {message}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Login error: {ex.Message}");
            }
        }

        private void HandleSuccessfulLogin(string username)
        {
            LogMessage($"✅ Login successful! Welcome {username}");
            LogMessage($"Role: {_apiService!.CurrentUser?.Role}");

            var allowedSites = _apiService.CurrentUser?.AllowedSites ?? new List<int>();
            LogMessage($"Allowed sites: {string.Join(", ", allowedSites)}");

            UpdateConnectionStatus();
        }

        private async Task LoadInitialData()
        {
            try
            {
                // Load sites and sensors after successful login
                await RefreshSitesData();
                await RefreshAlarmsData();
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error loading initial data: {ex.Message}");
            }
        }
        #endregion

        #region Data Retrieval
        private async void GetSites_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteWithButtonDisabled(GetSitesButton, async () =>
            {
                LogMessage("Getting sites from server...");

                if (!ValidateLoggedIn())
                    return;

                await RetrieveAndDisplaySites();
            });
        }

        private async void GetAlarms_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteWithButtonDisabled(GetAlarmsButton, async () =>
            {
                LogMessage("Getting alarms from server...");

                if (!ValidateLoggedIn())
                    return;

                await RetrieveAndDisplayAlarms();
            });
        }

        private bool ValidateLoggedIn()
        {
            if (_apiService == null || !_apiService.IsLoggedIn)
            {
                LogMessage("❌ Please login first!");
                return false;
            }
            return true;
        }

        private async Task RetrieveAndDisplaySites()
        {
            try
            {
                var sites = await _apiService!.GetSitesAsync();

                if (sites.Any())
                {
                    LogMessage($"✅ Retrieved {sites.Count} sites:");
                    DisplaySitesTable(sites);
                }
                else
                {
                    LogMessage("❌ No sites found or access denied!");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error getting sites: {ex.Message}");
            }
        }

        private async Task RetrieveAndDisplayAlarms()
        {
            try
            {
                var alarms = await _apiService!.GetAlarmsAsync(limit: 20);

                if (alarms.Any())
                {
                    LogMessage($"✅ Retrieved {alarms.Count} recent alarms:");
                    DisplayAlarmsTable(alarms);
                }
                else
                {
                    LogMessage("✅ No alarms found - system is healthy!");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error getting alarms: {ex.Message}");
            }
        }
        #endregion

        #region Data Display Methods
        private void DisplaySitesTable(List<Site> sites)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sites List:");
            sb.AppendLine("ID | Name                | County   | Status  | Sensors | Last Update");
            sb.AppendLine("---|---------------------|----------|---------|---------|------------");

            foreach (var site in sites)
            {
                sb.AppendLine($"{site.Id,2} | {site.Name,-19} | {site.County,-8} | {site.Status,-7} | {site.TotalSensors,7} | {site.LastUpdate:HH:mm:ss}");
            }

            LogMessage(sb.ToString());
        }

        private void DisplayAlarmsTable(List<Alarm> alarms)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Recent Alarms:");
            sb.AppendLine("Site | Sensor     | Message              | Time     | Age");
            sb.AppendLine("-----|------------|----------------------|----------|--------");

            foreach (var alarm in alarms.Take(10))
            {
                sb.AppendLine($"{alarm.SiteId,4} | {alarm.SensorTag,-10} | {alarm.AlarmMessage,-20} | {alarm.Timestamp:HH:mm:ss} | {alarm.MinutesAgo,3}min");
            }

            LogMessage(sb.ToString());
        }
        #endregion

        #region Simplified Map Navigation Methods
        private void SetupMapNavigation()
        {
            // Initialize with Romania overview
            ShowRomaniaOverview();
        }

        private void ShowRomaniaOverview()
        {
            RomaniaOverviewPanel.Visibility = Visibility.Visible;
            CountyDetailPanel.Visibility = Visibility.Collapsed;
            CurrentMapViewText.Text = "Current View: Romania Overview";

            UpdateMapStatistics();
        }

        private void ShowCountyDetail(string countyName)
        {
            RomaniaOverviewPanel.Visibility = Visibility.Collapsed;
            CountyDetailPanel.Visibility = Visibility.Visible;
            CurrentMapViewText.Text = $"Current View: {countyName} County";
            CountyDetailTitle.Text = $"📍 {countyName} County Sites";

            // Filter sites for this county
            var countySites = _sitesCollection.Where(s => s.County == countyName).ToList();
            CountySitesItemsControl.ItemsSource = countySites;
        }

        private void UpdateMapStatistics()
        {
            var totalSites = _sitesCollection.Count;
            var prahovaSites = _sitesCollection.Count(s => s.County == "Prahova");
            var gorjSites = _sitesCollection.Count(s => s.County == "Gorj");
            var totalAlarms = _sitesCollection.Sum(s => s.AlarmSensors);

            MapTotalSitesText.Text = totalSites.ToString();
            MapPrahovaSitesText.Text = prahovaSites.ToString();
            MapGorjSitesText.Text = gorjSites.ToString();
            MapActiveAlarmsText.Text = totalAlarms.ToString();

            PrahovaSitesCountText.Text = $"{prahovaSites} Sites";
            GorjSitesCountText.Text = $"{gorjSites} Sites";
        }

        private void UpdateMapViews()
        {
            // Update map statistics when sites data changes
            UpdateMapStatistics();
        }
        #endregion

        #region Simplified Map Event Handlers
        private void ShowRomaniaMap_Click(object sender, RoutedEventArgs e)
        {
            ShowRomaniaOverview();
            LogMessage("🗺️ Showing Romania overview");
        }

        private void ShowPrahovaMap_Click(object sender, RoutedEventArgs e)
        {
            ShowCountyDetail("Prahova");
            LogMessage("📍 Showing Prahova county sites");
        }

        private void ShowGorjMap_Click(object sender, RoutedEventArgs e)
        {
            ShowCountyDetail("Gorj");
            LogMessage("📍 Showing Gorj county sites");
        }

        private void PrahovaRegion_Click(object sender, MouseButtonEventArgs e)
        {
            ShowCountyDetail("Prahova");
            LogMessage("📍 Clicked Prahova region - showing sites");
        }

        private void GorjRegion_Click(object sender, MouseButtonEventArgs e)
        {
            ShowCountyDetail("Gorj");
            LogMessage("📍 Clicked Gorj region - showing sites");
        }

        private void BackToRomania_Click(object sender, RoutedEventArgs e)
        {
            ShowRomaniaOverview();
            LogMessage("🔙 Returned to Romania overview");
        }

        private async void SiteItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SiteViewModel site)
            {
                // Switch to Sensors tab and show sensors for this site
                _selectedSiteId = site.Id;
                SiteFilterComboBox.SelectedValue = site.Id;
                await RefreshSensorsData(site.Id);

                // Switch to Sensors tab
                foreach (TabItem tab in MainTabs.Items)
                {
                    if (tab.Header.ToString() == "Sensors")
                    {
                        tab.IsSelected = true;
                        break;
                    }
                }

                LogMessage($"🏭 Viewing sensors for site: {site.Name}");
            }
        }
        #endregion

        #region SignalR Event Handlers Setup
        private void SetupSignalREventHandlers()
        {
            if (_signalRService == null) return;

            // Connection status changes
            _signalRService.ConnectionStatusChanged += OnSignalRConnectionChanged;

            // Real-time data updates
            _signalRService.SensorUpdated += OnSensorUpdated;
            _signalRService.NewAlarm += OnNewAlarm;
            _signalRService.SubscriptionUpdated += OnSubscriptionUpdated;
        }

        private void OnSignalRConnectionChanged(object? sender, bool isConnected)
        {
            // Update UI on main thread
            Dispatcher.Invoke(() =>
            {
                SignalRStatus.Text = isConnected ? "Connected" : "Disconnected";
                SignalRStatus.Foreground = isConnected ? Brushes.Green : Brushes.Red;

                ConnectSignalRButton.IsEnabled = !isConnected;
                DisconnectSignalRButton.IsEnabled = isConnected;
                SubscribeButton.IsEnabled = isConnected;

                LogRealTimeMessage($"SignalR {(isConnected ? "connected" : "disconnected")}");
            });
        }

        private void OnSensorUpdated(object? sender, SensorUpdateEventArgs e)
        {
            // Update UI on main thread
            Dispatcher.Invoke(() =>
            {
                LogRealTimeMessage($"🔄 Sensor Update: {e.Data} at {e.Timestamp:HH:mm:ss.fff}");

                // Update sensor from real-time data
                UpdateSensorFromRealTimeData(e.Data);
            });
        }

        private void OnNewAlarm(object? sender, AlarmEventArgs e)
        {
            // Update UI on main thread
            Dispatcher.Invoke(() =>
            {
                LogRealTimeMessage($"🚨 NEW ALARM: {e.Data} at {e.Timestamp:HH:mm:ss.fff}");

                // Add new alarm to the collection
                AddNewAlarmFromRealTimeData(e.Data, e.Timestamp);

                // You could add sound notification here
                // SystemSounds.Exclamation.Play();
            });
        }

        private void OnSubscriptionUpdated(object? sender, List<int> siteIds)
        {
            Dispatcher.Invoke(() =>
            {
                LogRealTimeMessage($"✅ Subscribed to sites: {string.Join(", ", siteIds)}");
            });
        }

        private void UpdateSensorFromRealTimeData(object sensorData)
        {
            try
            {
                // This is a simplified version - you'd need to parse the actual JSON structure
                // For now, just trigger a refresh of sensor data if we have a selected site
                if (_selectedSiteId > 0)
                {
                    _ = Task.Run(async () => await RefreshSensorsData(_selectedSiteId));
                }
            }
            catch (Exception ex)
            {
                LogRealTimeMessage($"❌ Error updating sensor from real-time data: {ex.Message}");
            }
        }

        private void AddNewAlarmFromRealTimeData(object alarmData, DateTime timestamp)
        {
            try
            {
                // This is a simplified version - you'd need to parse the actual JSON structure
                // For now, just refresh the alarms data
                _ = Task.Run(async () => await RefreshAlarmsData());
            }
            catch (Exception ex)
            {
                LogRealTimeMessage($"❌ Error adding new alarm from real-time data: {ex.Message}");
            }
        }

        private void LogRealTimeMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}\n";

            RealTimeUpdatesTextBox.AppendText(logEntry);
            RealTimeUpdatesTextBox.ScrollToEnd();
        }
        #endregion

        #region SignalR Connection Management
        private async void ConnectSignalR_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteWithButtonDisabled(ConnectSignalRButton, async () =>
            {
                LogRealTimeMessage("Connecting to SignalR...");

                if (_signalRService == null)
                {
                    LogRealTimeMessage("❌ SignalR service not available");
                    return;
                }

                // Get auth token from current user
                var authToken = _apiService?.CurrentUser?.Token;

                var success = await _signalRService.ConnectAsync(authToken);

                if (!success)
                {
                    LogRealTimeMessage("❌ Failed to connect to SignalR");
                }
            });
        }

        private async void DisconnectSignalR_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteWithButtonDisabled(DisconnectSignalRButton, async () =>
            {
                LogRealTimeMessage("Disconnecting from SignalR...");

                if (_signalRService != null)
                {
                    await _signalRService.DisconnectAsync();
                }
            });
        }

        private async void Subscribe_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteWithButtonDisabled(SubscribeButton, async () =>
            {
                var siteIdsText = SiteSubscriptionTextBox.Text.Trim();

                if (string.IsNullOrEmpty(siteIdsText))
                {
                    LogRealTimeMessage("❌ Please enter site IDs to subscribe to");
                    return;
                }

                try
                {
                    // Parse site IDs
                    var siteIds = siteIdsText.Split(',')
                        .Select(s => int.Parse(s.Trim()))
                        .ToList();

                    LogRealTimeMessage($"Subscribing to sites: {string.Join(", ", siteIds)}");

                    if (_signalRService != null)
                    {
                        var success = await _signalRService.SubscribeToSitesAsync(siteIds);

                        if (!success)
                        {
                            LogRealTimeMessage("❌ Failed to subscribe to sites");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogRealTimeMessage($"❌ Error parsing site IDs: {ex.Message}");
                }
            });
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// Execute an async operation while disabling a button
        /// </summary>
        private async Task ExecuteWithButtonDisabled(Button button, Func<Task> operation)
        {
            try
            {
                button.IsEnabled = false;
                await operation();
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
        #endregion

        #region Window Events
        protected override void OnClosed(EventArgs e)
        {
            // Cleanup when window closes
            _autoRefreshTimer?.Stop();
            _signalRService?.Dispose();
            _apiService?.Logout();
            _apiService?.Dispose();
            base.OnClosed(e);
        }
        #endregion
    }
}