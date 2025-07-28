// File: Services/SignalRService.cs
// Real-time communication service - like PLC interrupt handling

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using GasFireMonitoringClient.Configuration;
using GasFireMonitoringClient.Models.Entities;

namespace GasFireMonitoringClient.Services
{
    /// <summary>
    /// SignalR service for real-time communication
    /// Think of this like PLC interrupt handling - responds immediately to server events
    /// </summary>
    public class SignalRService : IDisposable
    {
        #region Private Fields
        private readonly AppSettings _settings;
        private readonly ILogger<SignalRService>? _logger;
        private HubConnection? _connection;
        private bool _isConnecting = false;
        #endregion

        #region Events - Like PLC Interrupts
        /// <summary>
        /// Fired when connection status changes
        /// </summary>
        public event EventHandler<bool>? ConnectionStatusChanged;

        /// <summary>
        /// Fired when sensor data is updated - like analog input interrupt
        /// </summary>
        public event EventHandler<SensorUpdateEventArgs>? SensorUpdated;

        /// <summary>
        /// Fired when new alarm occurs - like alarm interrupt
        /// </summary>
        public event EventHandler<AlarmEventArgs>? NewAlarm;

        /// <summary>
        /// Fired when subscription is confirmed
        /// </summary>
        public event EventHandler<List<int>>? SubscriptionUpdated;
        #endregion

        #region Properties
        /// <summary>
        /// Current connection status
        /// </summary>
        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        /// <summary>
        /// Current connection state as string
        /// </summary>
        public string ConnectionState => _connection?.State.ToString() ?? "Disconnected";

        /// <summary>
        /// Sites currently subscribed to
        /// </summary>
        public List<int> SubscribedSites { get; private set; } = new();
        #endregion

        #region Constructor
        public SignalRService(AppSettings settings, ILogger<SignalRService>? logger = null)
        {
            _settings = settings;
            _logger = logger;
        }
        #endregion

        #region Connection Management
        /// <summary>
        /// Connect to SignalR hub
        /// </summary>
        public async Task<bool> ConnectAsync(string? authToken = null)
        {
            if (_isConnecting || IsConnected)
                return IsConnected;

            try
            {
                _isConnecting = true;
                _logger?.LogInformation("Connecting to SignalR hub...");

                // Build connection
                var connectionBuilder = new HubConnectionBuilder()
                    .WithUrl(_settings.SignalRHubUrl, options =>
                    {
                        // Add authentication if provided
                        if (!string.IsNullOrEmpty(authToken))
                        {
                            options.Headers.Add("Authorization", $"Bearer {authToken}");
                        }
                    })
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) });

                _connection = connectionBuilder.Build();

                // Set up event handlers
                SetupEventHandlers();

                // Connect
                await _connection.StartAsync();

                _logger?.LogInformation("Connected to SignalR hub successfully");
                ConnectionStatusChanged?.Invoke(this, true);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to connect to SignalR hub");
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }
            finally
            {
                _isConnecting = false;
            }
        }

        /// <summary>
        /// Disconnect from SignalR hub
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_connection != null)
            {
                try
                {
                    await _connection.StopAsync();
                    _logger?.LogInformation("Disconnected from SignalR hub");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error during SignalR disconnect");
                }
                finally
                {
                    ConnectionStatusChanged?.Invoke(this, false);
                }
            }
        }
        #endregion

        #region Event Handlers Setup
        private void SetupEventHandlers()
        {
            if (_connection == null) return;

            // Connection state change handlers
            _connection.Closed += OnConnectionClosed;
            _connection.Reconnecting += OnReconnecting;
            _connection.Reconnected += OnReconnected;

            // Server message handlers - like PLC interrupt handlers
            _connection.On<object>("SensorUpdate", OnSensorUpdate);
            _connection.On<object>("NewAlarm", OnNewAlarm);
            _connection.On<List<int>>("SubscriptionUpdated", OnSubscriptionUpdated);
        }

        private async Task OnConnectionClosed(Exception? exception)
        {
            _logger?.LogWarning(exception, "SignalR connection closed");
            ConnectionStatusChanged?.Invoke(this, false);
        }

        private async Task OnReconnecting(Exception? exception)
        {
            _logger?.LogInformation("SignalR reconnecting...");
            ConnectionStatusChanged?.Invoke(this, false);
        }

        private async Task OnReconnected(string? connectionId)
        {
            _logger?.LogInformation($"SignalR reconnected with ID: {connectionId}");
            ConnectionStatusChanged?.Invoke(this, true);

            // Re-subscribe to sites after reconnection
            if (SubscribedSites.Count > 0)
            {
                await SubscribeToSitesAsync(SubscribedSites);
            }
        }
        #endregion

        #region Server Message Handlers
        private void OnSensorUpdate(object sensorData)
        {
            try
            {
                _logger?.LogDebug("Received sensor update");

                // Create event args with the sensor data
                var eventArgs = new SensorUpdateEventArgs
                {
                    Data = sensorData,
                    Timestamp = DateTime.UtcNow
                };

                // Fire event - like triggering a PLC interrupt
                SensorUpdated?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing sensor update");
            }
        }

        private void OnNewAlarm(object alarmData)
        {
            try
            {
                _logger?.LogWarning("Received new alarm");

                // Create event args with the alarm data
                var eventArgs = new AlarmEventArgs
                {
                    Data = alarmData,
                    Timestamp = DateTime.UtcNow
                };

                // Fire event - like triggering an alarm interrupt
                NewAlarm?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing new alarm");
            }
        }

        private void OnSubscriptionUpdated(List<int> siteIds)
        {
            SubscribedSites = siteIds;
            _logger?.LogInformation($"Subscription updated: {string.Join(", ", siteIds)}");
            SubscriptionUpdated?.Invoke(this, siteIds);
        }
        #endregion

        #region Site Subscription Management
        /// <summary>
        /// Subscribe to updates from specific sites
        /// </summary>
        public async Task<bool> SubscribeToSitesAsync(List<int> siteIds)
        {
            if (_connection?.State != HubConnectionState.Connected)
            {
                _logger?.LogWarning("Cannot subscribe: not connected to SignalR hub");
                return false;
            }

            try
            {
                await _connection.InvokeAsync("SubscribeToSites", siteIds);
                _logger?.LogInformation($"Subscribed to sites: {string.Join(", ", siteIds)}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to subscribe to sites");
                return false;
            }
        }

        /// <summary>
        /// Unsubscribe from specific sites
        /// </summary>
        public async Task<bool> UnsubscribeFromSitesAsync(List<int> siteIds)
        {
            if (_connection?.State != HubConnectionState.Connected)
            {
                return false;
            }

            try
            {
                await _connection.InvokeAsync("UnsubscribeFromSites", siteIds);
                _logger?.LogInformation($"Unsubscribed from sites: {string.Join(", ", siteIds)}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to unsubscribe from sites");
                return false;
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            _connection?.DisposeAsync();
        }
        #endregion
    }

    #region Event Args Classes
    /// <summary>
    /// Event arguments for sensor updates
    /// </summary>
    public class SensorUpdateEventArgs : EventArgs
    {
        public object Data { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Event arguments for alarm notifications
    /// </summary>
    public class AlarmEventArgs : EventArgs
    {
        public object Data { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
    #endregion
}