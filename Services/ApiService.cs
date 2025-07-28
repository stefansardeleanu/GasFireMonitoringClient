// File: Services/ApiService.cs
// HTTP communication service - like an Ethernet driver for PLC

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GasFireMonitoringClient.Models.Entities;
using GasFireMonitoringClient.Configuration;

namespace GasFireMonitoringClient.Services
{
    /// <summary>
    /// Service for communicating with the server API
    /// Think of this like a PLC communication driver
    /// </summary>
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _settings;
        private UserInfo? _currentUser;

        public ApiService(AppSettings settings)
        {
            _settings = settings;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_settings.ServerBaseUrl),
                Timeout = TimeSpan.FromSeconds(_settings.HttpTimeoutSeconds)
            };
        }

        // Current logged-in user
        public UserInfo? CurrentUser => _currentUser;
        public bool IsLoggedIn => _currentUser?.IsLoggedIn == true;

        /// <summary>
        /// Test server connection - like testing PLC communication
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Login to server
        /// </summary>
        public async Task<(bool Success, string Message)> LoginAsync(string username, string password)
        {
            try
            {
                var loginRequest = new
                {
                    Username = username,
                    Password = password
                };

                var json = JsonSerializer.Serialize(loginRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/auth/login", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = JsonSerializer.Deserialize<ApiResponse<UserInfo>>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (loginResponse?.Success == true && loginResponse.Data != null)
                    {
                        _currentUser = loginResponse.Data;

                        // Add authorization header for future requests
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _currentUser.Token);

                        return (true, "Login successful");
                    }
                    else
                    {
                        return (false, loginResponse?.Message ?? "Login failed");
                    }
                }
                else
                {
                    return (false, $"Server error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all sites
        /// </summary>
        public async Task<List<Site>> GetSitesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/site");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<Site>>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return apiResponse?.Data ?? new List<Site>();
                }
            }
            catch (Exception ex)
            {
                // Log error (we'll add proper logging later)
                Console.WriteLine($"Error getting sites: {ex.Message}");
            }

            return new List<Site>();
        }

        /// <summary>
        /// Get sensors for a specific site
        /// </summary>
        public async Task<List<Sensor>> GetSensorsAsync(int siteId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/sensor/site/{siteId}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<Sensor>>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return apiResponse?.Data ?? new List<Sensor>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting sensors for site {siteId}: {ex.Message}");
            }

            return new List<Sensor>();
        }

        /// <summary>
        /// Get alarms
        /// </summary>
        public async Task<List<Alarm>> GetAlarmsAsync(int? siteId = null, int limit = 100)
        {
            try
            {
                var url = "/api/alarm";
                if (siteId.HasValue)
                {
                    url += $"?siteId={siteId}&limit={limit}";
                }
                else
                {
                    url += $"?limit={limit}";
                }

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonSerializer.Deserialize<ApiResponse<List<Alarm>>>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return apiResponse?.Data ?? new List<Alarm>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting alarms: {ex.Message}");
            }

            return new List<Alarm>();
        }

        /// <summary>
        /// Logout and cleanup
        /// </summary>
        public void Logout()
        {
            _currentUser = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}