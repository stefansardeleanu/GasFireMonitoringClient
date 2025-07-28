// File: Models/ViewModels/MonitoringViewModels.cs
// View models for data binding - like HMI data structures

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using GasFireMonitoringClient.Models.Entities;

namespace GasFireMonitoringClient.Models.ViewModels
{
    /// <summary>
    /// Base view model with property change notification
    /// </summary>
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// View model for site display with live updates
    /// </summary>
    public class SiteViewModel : BaseViewModel
    {
        private int _id;
        private string _name = "";
        private string _county = "";
        private string _status = "offline";
        private int _totalSensors;
        private int _normalSensors;
        private int _alarmSensors;
        private int _errorSensors;
        private DateTime _lastUpdate;
        private Brush _statusBrush = Brushes.Gray;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string County
        {
            get => _county;
            set => SetProperty(ref _county, value);
        }

        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    UpdateStatusBrush();
                }
            }
        }

        public int TotalSensors
        {
            get => _totalSensors;
            set => SetProperty(ref _totalSensors, value);
        }

        public int NormalSensors
        {
            get => _normalSensors;
            set => SetProperty(ref _normalSensors, value);
        }

        public int AlarmSensors
        {
            get => _alarmSensors;
            set => SetProperty(ref _alarmSensors, value);
        }

        public int ErrorSensors
        {
            get => _errorSensors;
            set => SetProperty(ref _errorSensors, value);
        }

        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set => SetProperty(ref _lastUpdate, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        // Computed properties
        public string LastUpdateText => LastUpdate == DateTime.MinValue ? "Never" : LastUpdate.ToString("HH:mm:ss");
        public string StatusIcon => Status switch
        {
            "normal" => "✅",
            "alarm" => "🚨",
            "error" => "❌",
            _ => "⚫"
        };

        private void UpdateStatusBrush()
        {
            StatusBrush = Status switch
            {
                "normal" => Brushes.Green,
                "alarm" => Brushes.Orange,
                "error" => Brushes.Red,
                _ => Brushes.Gray
            };
        }

        public void UpdateFromSite(Site site)
        {
            Id = site.Id;
            Name = site.Name;
            County = site.County;
            Status = site.Status;
            TotalSensors = site.TotalSensors;
            NormalSensors = site.TotalSensors - site.AlarmSensors;
            AlarmSensors = site.AlarmSensors;
            ErrorSensors = 0; // Will be calculated from actual sensor data
            LastUpdate = site.LastUpdate;
        }
    }

    /// <summary>
    /// View model for sensor display with live updates
    /// </summary>
    public class SensorViewModel : BaseViewModel
    {
        private int _id;
        private int _siteId;
        private string _siteName = "";
        private string _channelId = "";
        private string _tagName = "";
        private string _detectorTypeName = "";
        private double _processValue;
        private double _currentValue;
        private int _status;
        private string _statusText = "";
        private string _units = "";
        private DateTime _lastUpdated;
        private Brush _statusBrush = Brushes.Gray;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public int SiteId
        {
            get => _siteId;
            set => SetProperty(ref _siteId, value);
        }

        public string SiteName
        {
            get => _siteName;
            set => SetProperty(ref _siteName, value);
        }

        public string ChannelId
        {
            get => _channelId;
            set => SetProperty(ref _channelId, value);
        }

        public string TagName
        {
            get => _tagName;
            set => SetProperty(ref _tagName, value);
        }

        public string DetectorTypeName
        {
            get => _detectorTypeName;
            set => SetProperty(ref _detectorTypeName, value);
        }

        public double ProcessValue
        {
            get => _processValue;
            set => SetProperty(ref _processValue, value);
        }

        public double CurrentValue
        {
            get => _currentValue;
            set => SetProperty(ref _currentValue, value);
        }

        public int Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    UpdateStatusBrush();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string Units
        {
            get => _units;
            set => SetProperty(ref _units, value);
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        // Computed properties
        public string DisplayValue => $"{ProcessValue:F2} {Units}";
        public string LastUpdatedText => LastUpdated.ToString("HH:mm:ss");
        public bool IsOnline => (DateTime.UtcNow - LastUpdated).TotalMinutes < 5;
        public string OnlineStatus => IsOnline ? "Online" : "Offline";
        public string StatusIcon => Status switch
        {
            0 => "✅",  // Normal
            1 => "⚠️",  // Alarm Level 1
            2 => "🚨",  // Alarm Level 2
            _ => "❌"   // Error/Fault
        };

        private void UpdateStatusBrush()
        {
            StatusBrush = Status switch
            {
                0 => Brushes.Green,      // Normal
                1 => Brushes.Orange,     // Alarm Level 1
                2 => Brushes.Red,        // Alarm Level 2
                _ => Brushes.DarkRed     // Error/Fault
            };
        }

        public void UpdateFromSensor(Sensor sensor)
        {
            Id = sensor.Id;
            SiteId = sensor.SiteId;
            SiteName = sensor.SiteName;
            ChannelId = sensor.ChannelId;
            TagName = sensor.TagName;
            DetectorTypeName = GetDetectorTypeName(sensor.DetectorType);
            ProcessValue = sensor.ProcessValue;
            CurrentValue = sensor.CurrentValue;
            Status = sensor.Status;
            StatusText = sensor.StatusText;
            Units = sensor.Units;
            LastUpdated = sensor.LastUpdated;
        }

        private string GetDetectorTypeName(int type)
        {
            return type switch
            {
                1 => "Gas",
                2 => "Flame",
                3 => "Manual Call",
                4 => "Smoke",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// View model for alarm display
    /// </summary>
    public class AlarmViewModel : BaseViewModel
    {
        private int _id;
        private int _siteId;
        private string _siteName = "";
        private string _sensorTag = "";
        private string _alarmMessage = "";
        private DateTime _timestamp;
        private bool _isNew;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public int SiteId
        {
            get => _siteId;
            set => SetProperty(ref _siteId, value);
        }

        public string SiteName
        {
            get => _siteName;
            set => SetProperty(ref _siteName, value);
        }

        public string SensorTag
        {
            get => _sensorTag;
            set => SetProperty(ref _sensorTag, value);
        }

        public string AlarmMessage
        {
            get => _alarmMessage;
            set => SetProperty(ref _alarmMessage, value);
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        public bool IsNew
        {
            get => _isNew;
            set => SetProperty(ref _isNew, value);
        }

        // Computed properties
        public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public string TimeAgoText => GetTimeAgoText();
        public Brush BackgroundBrush => IsNew ? Brushes.LightYellow : Brushes.Transparent;

        private string GetTimeAgoText()
        {
            var timeSpan = DateTime.UtcNow - Timestamp;

            if (timeSpan.TotalMinutes < 1)
                return "Just now";
            else if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m ago";
            else if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h ago";
            else
                return $"{(int)timeSpan.TotalDays}d ago";
        }

        public void UpdateFromAlarm(Alarm alarm)
        {
            Id = alarm.Id;
            SiteId = alarm.SiteId;
            SiteName = alarm.SiteName;
            SensorTag = alarm.SensorTag;
            AlarmMessage = alarm.AlarmMessage;
            Timestamp = alarm.Timestamp;
        }
    }

    /// <summary>
    /// Main view model for the monitoring dashboard
    /// </summary>
    public class MonitoringDashboardViewModel : BaseViewModel
    {
        private bool _isConnected;
        private bool _isSignalRConnected;
        private string _connectionStatus = "Disconnected";
        private string _currentUser = "";
        private int _totalSites;
        private int _totalSensors;
        private int _activeSites;
        private int _activeAlarms;

        public ObservableCollection<SiteViewModel> Sites { get; } = new();
        public ObservableCollection<SensorViewModel> Sensors { get; } = new();
        public ObservableCollection<AlarmViewModel> RecentAlarms { get; } = new();

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public bool IsSignalRConnected
        {
            get => _isSignalRConnected;
            set => SetProperty(ref _isSignalRConnected, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public string CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        public int TotalSites
        {
            get => _totalSites;
            set => SetProperty(ref _totalSites, value);
        }

        public int TotalSensors
        {
            get => _totalSensors;
            set => SetProperty(ref _totalSensors, value);
        }

        public int ActiveSites
        {
            get => _activeSites;
            set => SetProperty(ref _activeSites, value);
        }

        public int ActiveAlarms
        {
            get => _activeAlarms;
            set => SetProperty(ref _activeAlarms, value);
        }
    }
}