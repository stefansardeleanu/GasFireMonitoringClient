// File: Models/ViewModels/MapViewModels.cs
// View models for map-based navigation

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using GasFireMonitoringClient.Models.Entities;

namespace GasFireMonitoringClient.Models.ViewModels
{
    /// <summary>
    /// View model for county representation on Romania map
    /// </summary>
    public class CountyViewModel : BaseViewModel
    {
        private string _name = "";
        private string _status = "offline";
        private int _totalSites;
        private int _normalSites;
        private int _alarmSites;
        private int _errorSites;
        private Brush _fillBrush = Brushes.Gray;
        private Brush _strokeBrush = Brushes.Black;
        private double _strokeThickness = 1.0;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    UpdateBrushes();
                }
            }
        }

        public int TotalSites
        {
            get => _totalSites;
            set => SetProperty(ref _totalSites, value);
        }

        public int NormalSites
        {
            get => _normalSites;
            set => SetProperty(ref _normalSites, value);
        }

        public int AlarmSites
        {
            get => _alarmSites;
            set => SetProperty(ref _alarmSites, value);
        }

        public int ErrorSites
        {
            get => _errorSites;
            set => SetProperty(ref _errorSites, value);
        }

        public Brush FillBrush
        {
            get => _fillBrush;
            private set => SetProperty(ref _fillBrush, value);
        }

        public Brush StrokeBrush
        {
            get => _strokeBrush;
            private set => SetProperty(ref _strokeBrush, value);
        }

        public double StrokeThickness
        {
            get => _strokeThickness;
            set => SetProperty(ref _strokeThickness, value);
        }

        // Computed properties
        public string StatusIcon => Status switch
        {
            "normal" => "✅",
            "alarm" => "🚨",
            "error" => "❌",
            _ => "⚫"
        };

        public string ToolTipText => $"{Name}\n" +
                                   $"Sites: {TotalSites}\n" +
                                   $"Normal: {NormalSites}\n" +
                                   $"Alarms: {AlarmSites}\n" +
                                   $"Status: {Status}";

        private void UpdateBrushes()
        {
            // Set fill color based on status
            FillBrush = Status switch
            {
                "normal" => new SolidColorBrush(Color.FromArgb(180, 0, 255, 0)),      // Semi-transparent green
                "alarm" => new SolidColorBrush(Color.FromArgb(180, 255, 165, 0)),     // Semi-transparent orange
                "error" => new SolidColorBrush(Color.FromArgb(180, 255, 0, 0)),       // Semi-transparent red
                _ => new SolidColorBrush(Color.FromArgb(180, 128, 128, 128))          // Semi-transparent gray
            };

            // Set stroke color (border)
            StrokeBrush = Status switch
            {
                "normal" => Brushes.DarkGreen,
                "alarm" => Brushes.DarkOrange,
                "error" => Brushes.DarkRed,
                _ => Brushes.DarkGray
            };

            // Set stroke thickness based on status (thicker for alerts)
            StrokeThickness = Status switch
            {
                "alarm" => 2.5,
                "error" => 3.0,
                _ => 1.5
            };
        }

        public void UpdateFromSites(ObservableCollection<SiteViewModel> sites)
        {
            var countySites = sites.Where(s => s.County == Name).ToList();

            TotalSites = countySites.Count;
            NormalSites = countySites.Count(s => s.Status == "normal");
            AlarmSites = countySites.Count(s => s.Status == "alarm");
            ErrorSites = countySites.Count(s => s.Status == "error");

            // Determine overall county status
            if (ErrorSites > 0)
                Status = "error";
            else if (AlarmSites > 0)
                Status = "alarm";
            else if (NormalSites > 0)
                Status = "normal";
            else
                Status = "offline";
        }
    }

    /// <summary>
    /// View model for site representation on county map
    /// </summary>
    public class SiteMapViewModel : BaseViewModel
    {
        private int _id;
        private string _name = "";
        private string _county = "";
        private double _latitude;
        private double _longitude;
        private string _status = "offline";
        private int _totalSensors;
        private int _alarmSensors;
        private double _mapX;
        private double _mapY;
        private Brush _fillBrush = Brushes.Gray;
        private double _markerSize = 12;

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

        public double Latitude
        {
            get => _latitude;
            set => SetProperty(ref _latitude, value);
        }

        public double Longitude
        {
            get => _longitude;
            set => SetProperty(ref _longitude, value);
        }

        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    UpdateAppearance();
                }
            }
        }

        public int TotalSensors
        {
            get => _totalSensors;
            set => SetProperty(ref _totalSensors, value);
        }

        public int AlarmSensors
        {
            get => _alarmSensors;
            set => SetProperty(ref _alarmSensors, value);
        }

        public double MapX
        {
            get => _mapX;
            set => SetProperty(ref _mapX, value);
        }

        public double MapY
        {
            get => _mapY;
            set => SetProperty(ref _mapY, value);
        }

        public Brush FillBrush
        {
            get => _fillBrush;
            private set => SetProperty(ref _fillBrush, value);
        }

        public double MarkerSize
        {
            get => _markerSize;
            private set => SetProperty(ref _markerSize, value);
        }

        // Computed properties
        public string StatusIcon => Status switch
        {
            "normal" => "✅",
            "alarm" => "🚨",
            "error" => "❌",
            _ => "⚫"
        };

        public string ToolTipText => $"{Name}\n" +
                                   $"County: {County}\n" +
                                   $"Sensors: {TotalSensors}\n" +
                                   $"Alarms: {AlarmSensors}\n" +
                                   $"Status: {Status}\n" +
                                   $"Coordinates: {Latitude:F4}, {Longitude:F4}";

        private void UpdateAppearance()
        {
            // Set fill color and size based on status
            (FillBrush, MarkerSize) = Status switch
            {
                "normal" => (Brushes.Green, 12),
                "alarm" => (Brushes.Orange, 15),
                "error" => (Brushes.Red, 18),
                _ => (Brushes.Gray, 10)
            };
        }

        public void UpdateFromSite(SiteViewModel site)
        {
            Id = site.Id;
            Name = site.Name;
            County = site.County;
            Status = site.Status;
            TotalSensors = site.TotalSensors;
            AlarmSensors = site.AlarmSensors;
            // Note: Latitude/Longitude would come from Site entity if available
        }

        /// <summary>
        /// Convert geographic coordinates to map canvas coordinates
        /// This is a simplified conversion - you'd need proper map projection
        /// </summary>
        public void UpdateMapPosition(double canvasWidth, double canvasHeight,
            double minLat, double maxLat, double minLon, double maxLon)
        {
            // Simple linear mapping - in real implementation you'd use proper map projection
            MapX = (Longitude - minLon) / (maxLon - minLon) * canvasWidth;
            MapY = (maxLat - Latitude) / (maxLat - minLat) * canvasHeight; // Flip Y axis
        }
    }

    /// <summary>
    /// View model for navigation breadcrumb
    /// </summary>
    public class BreadcrumbViewModel : BaseViewModel
    {
        private string _text = "";
        private bool _isClickable = true;
        private object? _navigationTarget;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public bool IsClickable
        {
            get => _isClickable;
            set => SetProperty(ref _isClickable, value);
        }

        public object? NavigationTarget
        {
            get => _navigationTarget;
            set => SetProperty(ref _navigationTarget, value);
        }

        public Brush TextBrush => IsClickable ? Brushes.Blue : Brushes.Black;
        public string Cursor => IsClickable ? "Hand" : "Arrow";
    }

    /// <summary>
    /// Main view model for map navigation
    /// </summary>
    public class MapNavigationViewModel : BaseViewModel
    {
        private string _currentView = "Romania";
        private string _selectedCounty = "";
        private int _selectedSiteId = -1;
        private bool _isLoading = false;

        public ObservableCollection<CountyViewModel> Counties { get; } = new();
        public ObservableCollection<SiteMapViewModel> CountySites { get; } = new();
        public ObservableCollection<BreadcrumbViewModel> Breadcrumbs { get; } = new();

        public string CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public string SelectedCounty
        {
            get => _selectedCounty;
            set => SetProperty(ref _selectedCounty, value);
        }

        public int SelectedSiteId
        {
            get => _selectedSiteId;
            set => SetProperty(ref _selectedSiteId, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // View visibility properties
        public bool IsRomaniaViewVisible => CurrentView == "Romania";
        public bool IsCountyViewVisible => CurrentView == "County";
        public bool IsSiteViewVisible => CurrentView == "Site";

        public void NavigateToRomania()
        {
            CurrentView = "Romania";
            SelectedCounty = "";
            SelectedSiteId = -1;
            UpdateBreadcrumbs();
        }

        public void NavigateToCounty(string countyName)
        {
            CurrentView = "County";
            SelectedCounty = countyName;
            SelectedSiteId = -1;
            UpdateBreadcrumbs();
        }

        public void NavigateToSite(int siteId)
        {
            CurrentView = "Site";
            SelectedSiteId = siteId;
            UpdateBreadcrumbs();
        }

        private void UpdateBreadcrumbs()
        {
            Breadcrumbs.Clear();

            // Romania level
            Breadcrumbs.Add(new BreadcrumbViewModel
            {
                Text = "🗺️ Romania",
                IsClickable = CurrentView != "Romania",
                NavigationTarget = "Romania"
            });

            // County level
            if (!string.IsNullOrEmpty(SelectedCounty))
            {
                Breadcrumbs.Add(new BreadcrumbViewModel
                {
                    Text = $"📍 {SelectedCounty}",
                    IsClickable = CurrentView != "County",
                    NavigationTarget = SelectedCounty
                });
            }

            // Site level
            if (SelectedSiteId > 0)
            {
                // You'd get the site name from your data
                Breadcrumbs.Add(new BreadcrumbViewModel
                {
                    Text = $"🏭 Site {SelectedSiteId}",
                    IsClickable = false,
                    NavigationTarget = SelectedSiteId
                });
            }
        }

        public void InitializeCounties()
        {
            // Initialize all Romanian counties
            var romanianCounties = new[]
            {
                "Alba", "Arad", "Argeș", "Bacău", "Bihor", "Bistrița-Năsăud", "Botoșani", "Brașov",
                "Brăila", "Buzău", "Caraș-Severin", "Călărași", "Cluj", "Constanța", "Covasna",
                "Dâmbovița", "Dolj", "Galați", "Giurgiu", "Gorj", "Harghita", "Hunedoara", "Ialomița",
                "Iași", "Ilfov", "Maramureș", "Mehedinți", "Mureș", "Neamț", "Olt", "Prahova",
                "Satu Mare", "Sălaj", "Sibiu", "Suceava", "Teleorman", "Timiș", "Tulcea", "Vaslui",
                "Vâlcea", "Vrancea", "București"
            };

            Counties.Clear();
            foreach (var county in romanianCounties)
            {
                Counties.Add(new CountyViewModel
                {
                    Name = county,
                    Status = "offline",
                    TotalSites = 0
                });
            }
        }
    }
}