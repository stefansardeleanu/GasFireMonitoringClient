// File: Models/Entities/BasicModels.cs
// Basic data structures - like PLC data types

using System;
using System.Collections.Generic;

namespace GasFireMonitoringClient.Models.Entities
{
    /// <summary>
    /// Represents a monitoring site - like a PLC station
    /// </summary>
    public class Site
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string County { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Status { get; set; } = "offline"; // normal, alarm, error, offline
        public int TotalSensors { get; set; }
        public int AlarmSensors { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>
    /// Represents a sensor - like an analog input
    /// </summary>
    public class Sensor
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public string SiteName { get; set; } = "";
        public string ChannelId { get; set; } = "";
        public string TagName { get; set; } = "";
        public int DetectorType { get; set; }
        public double ProcessValue { get; set; }
        public double CurrentValue { get; set; }
        public int Status { get; set; }
        public string StatusText { get; set; } = "";
        public string Units { get; set; } = "";
        public DateTime LastUpdated { get; set; }

        // Computed properties
        public bool IsOnline => (DateTime.UtcNow - LastUpdated).TotalMinutes < 5;
        public bool HasAlarm => Status > 0 && Status <= 2;
        public bool HasError => Status > 2;
    }

    /// <summary>
    /// Represents an alarm - like a PLC alarm message
    /// </summary>
    public class Alarm
    {
        public int Id { get; set; }
        public int SiteId { get; set; }
        public string SiteName { get; set; } = "";
        public string SensorTag { get; set; } = "";
        public string AlarmMessage { get; set; } = "";
        public string RawMessage { get; set; } = "";
        public DateTime Timestamp { get; set; }

        // Computed properties
        public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public int MinutesAgo => (int)(DateTime.UtcNow - Timestamp).TotalMinutes;
    }

    /// <summary>
    /// User login information
    /// </summary>
    public class UserInfo
    {
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public string Token { get; set; } = "";
        public List<string> AllowedCounties { get; set; } = new();
        public List<int> AllowedSites { get; set; } = new();
        public bool IsLoggedIn => !string.IsNullOrEmpty(Token);
    }

    /// <summary>
    /// API response wrapper
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public T? Data { get; set; }
        public int Count { get; set; }
    }
}