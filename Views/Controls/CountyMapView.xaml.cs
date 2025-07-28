// File: Views/Controls/CountyMapView.xaml.cs
// County map showing sites with coordinates

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GasFireMonitoringClient.Models.ViewModels;

namespace GasFireMonitoringClient.Views.Controls
{
    /// <summary>
    /// County map showing sites positioned by coordinates
    /// </summary>
    public partial class CountyMapView : UserControl
    {
        #region Events
        /// <summary>
        /// Event fired when a site is clicked
        /// </summary>
        public event EventHandler<int>? SiteClicked;
        #endregion

        #region Private Fields
        private string _currentCounty = "";
        private ObservableCollection<SiteMapViewModel> _sites = new();
        #endregion

        #region Constructor
        public CountyMapView()
        {
            InitializeComponent();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Load sites for a specific county
        /// </summary>
        public void LoadCountySites(string countyName, ObservableCollection<SiteViewModel> allSites)
        {
            try
            {
                _currentCounty = countyName;
                CountyTitleText.Text = $"📍 {countyName} County - Sites Overview";

                // Filter sites for this county
                var countySites = allSites.Where(s => s.County == countyName).ToList();

                // Convert to map view models
                _sites.Clear();
                foreach (var site in countySites)
                {
                    var siteMap = new SiteMapViewModel();
                    siteMap.UpdateFromSite(site);

                    // Set coordinates (you would get these from your database)
                    SetSiteCoordinates(siteMap);

                    _sites.Add(siteMap);
                }

                // Update county background shape
                UpdateCountyBackground(countyName);

                // Position sites on map
                PositionSitesOnMap();

                // Update statistics
                UpdateCountyStatistics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading county sites: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh site status (for real-time updates)
        /// </summary>
        public void RefreshSiteStatus(ObservableCollection<SiteViewModel> allSites)
        {
            try
            {
                foreach (var siteMap in _sites)
                {
                    var updatedSite = allSites.FirstOrDefault(s => s.Id == siteMap.Id);
                    if (updatedSite != null)
                    {
                        siteMap.UpdateFromSite(updatedSite);
                        UpdateSiteMarker(siteMap);
                    }
                }

                UpdateCountyStatistics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing site status: {ex.Message}");
            }
        }
        #endregion

        #region Private Methods
        private void SetSiteCoordinates(SiteMapViewModel site)
        {
            // Set sample coordinates for your known sites
            // In production, these would come from your database
            var siteCoordinates = new Dictionary<string, (double lat, double lon)>
            {
                { "SondaMorEni", (44.9344, 26.0137) },
                { "SondaArb", (44.8567, 25.9876) },
                { "SondaBerPH01", (44.9123, 26.0456) },
                { "ParcMorMic", (44.9012, 26.0234) },
                { "PanouHurezani", (44.8925, 23.3731) },
                { "PanouZatreni", (44.9123, 23.3456) },
                { "ParcBatrani", (44.8789, 26.0123) },
                { "ParcCartojani", (44.8678, 25.9987) },
                { "ParcTintea", (44.8901, 26.0345) },
                { "StatieLucacesti", (44.9234, 26.0567) }
            };

            if (siteCoordinates.ContainsKey(site.Name))
            {
                var (lat, lon) = siteCoordinates[site.Name];
                site.Latitude = lat;
                site.Longitude = lon;
            }
            else
            {
                // Default coordinates if not found
                site.Latitude = 44.9;
                site.Longitude = _currentCounty == "Prahova" ? 26.0 : 23.3;
            }
        }

        private void UpdateCountyBackground(string countyName)
        {
            // Set county background shape based on county
            // This is simplified - you would use actual county boundary data
            var points = countyName switch
            {
                "Prahova" => "100,100 700,120 750,300 700,500 150,480 80,300",
                "Gorj" => "100,150 650,170 680,400 620,520 120,500 60,350",
                _ => "100,100 700,100 700,500 100,500"
            };

            CountyBackground.Points = PointCollection.Parse(points);
        }

        private void PositionSitesOnMap()
        {
            // Clear existing site markers
            var existingMarkers = CountyMapCanvas.Children.OfType<FrameworkElement>()
                .Where(e => e.Tag is SiteMapViewModel).ToList();

            foreach (var marker in existingMarkers)
            {
                CountyMapCanvas.Children.Remove(marker);
            }

            if (!_sites.Any()) return;

            // Calculate coordinate bounds
            var minLat = _sites.Min(s => s.Latitude);
            var maxLat = _sites.Max(s => s.Latitude);
            var minLon = _sites.Min(s => s.Longitude);
            var maxLon = _sites.Max(s => s.Longitude);

            // Add padding
            var latPadding = (maxLat - minLat) * 0.1;
            var lonPadding = (maxLon - minLon) * 0.1;

            minLat -= latPadding;
            maxLat += latPadding;
            minLon -= lonPadding;
            maxLon += lonPadding;

            // Position each site
            foreach (var site in _sites)
            {
                site.UpdateMapPosition(CountyMapCanvas.Width, CountyMapCanvas.Height,
                    minLat, maxLat, minLon, maxLon);

                CreateSiteMarker(site);
            }
        }

        private void CreateSiteMarker(SiteMapViewModel site)
        {
            // Create site marker (circle)
            var marker = new Ellipse
            {
                Width = site.MarkerSize,
                Height = site.MarkerSize,
                Fill = site.FillBrush,
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Cursor = Cursors.Hand,
                Tag = site,
                ToolTip = site.ToolTipText
            };

            // Position marker
            Canvas.SetLeft(marker, site.MapX - site.MarkerSize / 2);
            Canvas.SetTop(marker, site.MapY - site.MarkerSize / 2);

            // Add click handler
            marker.MouseLeftButtonDown += SiteMarker_Click;
            marker.MouseEnter += SiteMarker_MouseEnter;
            marker.MouseLeave += SiteMarker_MouseLeave;

            // Add to canvas
            CountyMapCanvas.Children.Add(marker);

            // Create site label
            var label = new TextBlock
            {
                Text = site.Name,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Background = Brushes.White,
                Padding = new Thickness(2),
                Tag = site,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(label, site.MapX + site.MarkerSize / 2 + 5);
            Canvas.SetTop(label, site.MapY - 8);

            CountyMapCanvas.Children.Add(label);
        }

        private void UpdateSiteMarker(SiteMapViewModel site)
        {
            // Find and update existing marker
            var marker = CountyMapCanvas.Children.OfType<Ellipse>()
                .FirstOrDefault(e => e.Tag == site);

            if (marker != null)
            {
                marker.Width = site.MarkerSize;
                marker.Height = site.MarkerSize;
                marker.Fill = site.FillBrush;
                marker.ToolTip = site.ToolTipText;
            }
        }

        private void UpdateCountyStatistics()
        {
            var totalSites = _sites.Count;
            var normalSites = _sites.Count(s => s.Status == "normal");
            var alarmSites = _sites.Count(s => s.Status == "alarm");
            var errorSites = _sites.Count(s => s.Status == "error");

            CountyTotalSitesText.Text = totalSites.ToString();
            CountyNormalSitesText.Text = normalSites.ToString();
            CountyAlarmSitesText.Text = alarmSites.ToString();
            CountyErrorSitesText.Text = errorSites.ToString();
        }
        #endregion

        #region Event Handlers
        private void SiteMarker_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is SiteMapViewModel site)
            {
                SiteClicked?.Invoke(this, site.Id);
            }
        }

        private void SiteMarker_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Ellipse marker)
            {
                // Enlarge marker on hover
                marker.Width += 4;
                marker.Height += 4;
                marker.StrokeThickness = 3;

                // Adjust position to keep centered
                var left = Canvas.GetLeft(marker) - 2;
                var top = Canvas.GetTop(marker) - 2;
                Canvas.SetLeft(marker, left);
                Canvas.SetTop(marker, top);
            }
        }

        private void SiteMarker_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Ellipse marker && marker.Tag is SiteMapViewModel site)
            {
                // Restore original size
                marker.Width = site.MarkerSize;
                marker.Height = site.MarkerSize;
                marker.StrokeThickness = 2;

                // Restore position
                Canvas.SetLeft(marker, site.MapX - site.MarkerSize / 2);
                Canvas.SetTop(marker, site.MapY - site.MarkerSize / 2);
            }
        }
        #endregion
    }
}