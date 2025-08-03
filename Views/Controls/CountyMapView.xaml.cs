// File: Views/Controls/CountyMapView.xaml.cs
// Self-contained county map control with server API integration

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml;
using System.Collections.Generic;
using System.Threading.Tasks;
using GasFireMonitoringClient.Models.ViewModels;
using GasFireMonitoringClient.Services;

// Alias to avoid ambiguity with System.IO.Path
using WpfPath = System.Windows.Shapes.Path;

namespace GasFireMonitoringClient.Views.Controls
{
    /// <summary>
    /// Self-contained county map control with sites and status indicators
    /// </summary>
    public partial class CountyMapView : UserControl
    {
        #region Events
        /// <summary>
        /// Event fired when user wants to go back to main map
        /// </summary>
        public event EventHandler? BackToMainMapRequested;
        #endregion

        #region Private Fields
        private string _currentCounty = "";
        private ObservableCollection<SiteMapViewModel> _sites = new();
        private WpfPath _countyPath;
        private TransformGroup _mapTransform;
        private Dictionary<int, FrameworkElement> _siteMarkers = new();
        private ApiService _apiService;
        private bool _isLoading = false;

        // Zoom functionality
        private double _currentZoom = 1.0;
        private const double ZOOM_FACTOR = 1.2;
        private const double MIN_ZOOM = 0.5;
        private const double MAX_ZOOM = 3.0;
        #endregion

        #region Constructor
        public CountyMapView()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
            CreateNavigationHeader();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initialize with API service
        /// </summary>
        public void Initialize(ApiService apiService)
        {
            _apiService = apiService;
        }

        /// <summary>
        /// Load and display a county with its sites
        /// </summary>
        public async void LoadCounty(string countyName, ObservableCollection<SiteViewModel> allSites)
        {
            try
            {
                _currentCounty = countyName;
                ShowLoadingIndicator(true);

                // Load county shape from SVG
                LoadCountyShape(countyName);

                // Load sites and their positions from server
                await LoadCountySitesWithServerData(countyName, allSites);

                ShowLoadingIndicator(false);
            }
            catch (Exception ex)
            {
                ShowLoadingIndicator(false);
                System.Diagnostics.Debug.WriteLine($"Error loading county: {ex.Message}");
                MessageBox.Show($"Error loading county map: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

        #region Navigation Header
        private void CreateNavigationHeader()
        {
            var headerPanel = this.FindName("HeaderPanel") as Panel;
            if (headerPanel == null)
            {
                var grid = Content as Grid;
                if (grid != null && grid.RowDefinitions.Count > 0)
                {
                    headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    Grid.SetRow(headerPanel as UIElement, 0);
                    grid.Children.Insert(0, headerPanel as UIElement);
                }
            }
        }
        #endregion

        #region Loading Indicator
        private void ShowLoadingIndicator(bool show)
        {
            _isLoading = show;

            // Create or find loading overlay
            var loadingOverlay = CountyMapCanvas.Children.OfType<Border>()
                .FirstOrDefault(b => b.Name == "LoadingOverlay");

            if (show)
            {
                if (loadingOverlay == null)
                {
                    loadingOverlay = new Border
                    {
                        Name = "LoadingOverlay",
                        Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                        Width = CountyMapCanvas.ActualWidth > 0 ? CountyMapCanvas.ActualWidth : 800,
                        Height = CountyMapCanvas.ActualHeight > 0 ? CountyMapCanvas.ActualHeight : 600
                    };

                    var loadingPanel = new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    loadingPanel.Children.Add(new TextBlock
                    {
                        Text = "Loading site positions from server...",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 10)
                    });

                    loadingPanel.Children.Add(new ProgressBar
                    {
                        IsIndeterminate = true,
                        Width = 200,
                        Height = 20
                    });

                    loadingOverlay.Child = loadingPanel;
                    Canvas.SetZIndex(loadingOverlay, 9999);
                    CountyMapCanvas.Children.Add(loadingOverlay);
                }
            }
            else if (loadingOverlay != null)
            {
                CountyMapCanvas.Children.Remove(loadingOverlay);
            }
        }
        #endregion

        #region Server Data Loading
        private async Task LoadCountySitesWithServerData(string countyName, ObservableCollection<SiteViewModel> allSites)
        {
            try
            {
                // Filter sites for this county
                var countySites = allSites.Where(s => s.County == countyName).ToList();

                // Clear existing sites
                _sites.Clear();

                if (_apiService == null)
                {
                    throw new InvalidOperationException("ApiService not initialized. Call Initialize() first.");
                }

                // Get site details from server (includes coordinates)
                var serverSites = await _apiService.GetSitesAsync();

                foreach (var localSite in countySites)
                {
                    var siteMap = new SiteMapViewModel();
                    siteMap.UpdateFromSite(localSite);

                    // Find matching server site to get coordinates
                    var serverSite = serverSites.FirstOrDefault(s => s.Name == localSite.Name);
                    if (serverSite != null)
                    {
                        siteMap.Latitude = serverSite.Latitude;
                        siteMap.Longitude = serverSite.Longitude;

                        System.Diagnostics.Debug.WriteLine($"Site {siteMap.Name}: Lat={siteMap.Latitude}, Lon={siteMap.Longitude}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: No server data found for site {localSite.Name}");
                    }

                    _sites.Add(siteMap);
                }

                // Position sites on map using their coordinates
                PositionSitesOnMap();

                // Update statistics
                UpdateCountyStatistics();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load site data from server: {ex.Message}", ex);
            }
        }

        private void PositionSitesOnMap()
        {
            // Clear existing markers
            _siteMarkers.Clear();
            var existingMarkers = CountyMapCanvas.Children.OfType<FrameworkElement>()
                .Where(e => e.Tag is SiteMapViewModel).ToList();

            foreach (var marker in existingMarkers)
            {
                CountyMapCanvas.Children.Remove(marker);
            }

            if (!_sites.Any()) return;

            // Get sites with valid coordinates
            var sitesWithCoordinates = _sites.Where(s => s.Latitude != 0 && s.Longitude != 0).ToList();

            if (sitesWithCoordinates.Any())
            {
                var canvasWidth = CountyMapCanvas.ActualWidth > 0 ? CountyMapCanvas.ActualWidth : 800;
                var canvasHeight = CountyMapCanvas.ActualHeight > 0 ? CountyMapCanvas.ActualHeight : 600;

                // Position each site based on its coordinates
                foreach (var site in _sites)
                {
                    if (site.Latitude != 0 && site.Longitude != 0)
                    {
                        // Convert geographic coordinates to canvas coordinates
                        site.UpdateMapPosition(canvasWidth, canvasHeight);
                    }
                    else
                    {
                        // Fallback for sites without coordinates
                        var index = _sites.ToList().IndexOf(site);
                        site.MapX = 100 + (index % 3) * 250;
                        site.MapY = 100 + (index / 3) * 150;
                    }

                    CreateSiteMarker(site);
                }
            }
            else
            {
                // Fallback grid layout if no coordinates available
                System.Diagnostics.Debug.WriteLine("Warning: No sites have valid coordinates, using grid layout");

                foreach (var site in _sites)
                {
                    var index = _sites.ToList().IndexOf(site);
                    site.MapX = 100 + (index % 3) * 250;
                    site.MapY = 100 + (index / 3) * 150;

                    CreateSiteMarker(site);
                }
            }
        }
        #endregion

        #region SVG Loading
        private void LoadCountyShape(string countyName)
        {
            try
            {
                var existingPaths = CountyMapCanvas.Children.OfType<WpfPath>().ToList();
                foreach (var path in existingPaths)
                {
                    CountyMapCanvas.Children.Remove(path);
                }

                var svgContent = LoadSvgFromResources();
                if (string.IsNullOrEmpty(svgContent))
                {
                    CreateFallbackCountyShape(countyName);
                    return;
                }

                var pathData = ExtractCountyPath(svgContent, countyName);
                if (!string.IsNullOrEmpty(pathData))
                {
                    _countyPath = new WpfPath
                    {
                        Data = Geometry.Parse(pathData),
                        Fill = new SolidColorBrush(Color.FromArgb(30, 200, 200, 200)),
                        Stroke = Brushes.DarkGray,
                        StrokeThickness = 2,
                        IsHitTestVisible = false
                    };

                    CountyMapCanvas.Children.Insert(0, _countyPath);
                    ScaleCountyToFit();
                }
                else
                {
                    CreateFallbackCountyShape(countyName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading county shape: {ex.Message}");
                CreateFallbackCountyShape(countyName);
            }
        }

        private string LoadSvgFromResources()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();
                var svgResourceName = resourceNames.FirstOrDefault(r => r.Contains("romania_map.svg"));

                if (svgResourceName == null) return "";

                using (var stream = assembly.GetManifestResourceStream(svgResourceName))
                using (var reader = new System.IO.StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                return "";
            }
        }

        private string ExtractCountyPath(string svgContent, string countyName)
        {
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(svgContent);

                var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                nsmgr.AddNamespace("svg", "http://www.w3.org/2000/svg");

                var pathNodes = xmlDoc.SelectNodes("//svg:path", nsmgr);
                if (pathNodes == null) return "";

                foreach (XmlNode node in pathNodes)
                {
                    var nameAttr = node.Attributes?["name"]?.Value;
                    var idAttr = node.Attributes?["id"]?.Value;

                    if (nameAttr?.Contains(countyName, StringComparison.OrdinalIgnoreCase) == true ||
                        idAttr?.Contains(countyName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return node.Attributes?["d"]?.Value ?? "";
                    }
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private void CreateFallbackCountyShape(string countyName)
        {
            var points = countyName switch
            {
                "Prahova" => "100,100 700,120 750,300 700,500 150,480 80,300",
                "Gorj" => "100,150 650,170 680,400 620,520 120,500 60,350",
                _ => "100,100 700,100 700,500 100,500"
            };

            _countyPath = new WpfPath
            {
                Data = Geometry.Parse($"M {points} Z"),
                Fill = new SolidColorBrush(Color.FromArgb(30, 200, 200, 200)),
                Stroke = Brushes.DarkGray,
                StrokeThickness = 2,
                IsHitTestVisible = false
            };

            CountyMapCanvas.Children.Insert(0, _countyPath);
        }

        private void ScaleCountyToFit()
        {
            if (_countyPath?.Data == null) return;

            var bounds = _countyPath.Data.Bounds;
            var canvasWidth = CountyMapCanvas.ActualWidth > 0 ? CountyMapCanvas.ActualWidth : 800;
            var canvasHeight = CountyMapCanvas.ActualHeight > 0 ? CountyMapCanvas.ActualHeight : 600;

            var scaleX = (canvasWidth - 40) / bounds.Width;
            var scaleY = (canvasHeight - 40) / bounds.Height;
            var scale = Math.Min(scaleX, scaleY) * 0.9;

            _mapTransform = new TransformGroup();
            _mapTransform.Children.Add(new ScaleTransform(scale, scale));
            _mapTransform.Children.Add(new TranslateTransform(
                (canvasWidth - bounds.Width * scale) / 2 - bounds.X * scale,
                (canvasHeight - bounds.Height * scale) / 2 - bounds.Y * scale
            ));

            _countyPath.RenderTransform = _mapTransform;
        }
        #endregion

        #region Site Markers
        private void CreateSiteMarker(SiteMapViewModel site)
        {
            var container = new Grid
            {
                Width = 80,
                Height = 35,
                Tag = site,
                Cursor = Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            var background = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 2,
                    BlurRadius = 4,
                    Opacity = 0.3
                }
            };

            var indicatorsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2)
            };

            // Calculate sensor counts
            var normalCount = site.TotalSensors - site.AlarmSensors;
            var errorCount = site.Status == "error" ? 1 : 0;
            var disabledCount = 0;

            var alarmSquare = CreateStatusSquare(
                site.AlarmSensors > 0 ? Brushes.Red : Brushes.LightGray,
                $"Alarms: {site.AlarmSensors}");

            var faultSquare = CreateStatusSquare(
                site.Status == "error" ? Brushes.Orange : Brushes.LightGray,
                $"Faults: {errorCount}");

            var disabledSquare = CreateStatusSquare(
                Brushes.LightGray,
                $"Disabled: {disabledCount}");

            var normalSquare = CreateStatusSquare(
                normalCount > 0 ? Brushes.Green : Brushes.LightGray,
                $"Normal: {normalCount}");

            indicatorsPanel.Children.Add(alarmSquare);
            indicatorsPanel.Children.Add(faultSquare);
            indicatorsPanel.Children.Add(disabledSquare);
            indicatorsPanel.Children.Add(normalSquare);

            var nameLabel = new TextBlock
            {
                Text = site.Name,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, -12)
            };

            container.Children.Add(background);
            container.Children.Add(indicatorsPanel);
            container.Children.Add(nameLabel);

            // Event handlers
            container.MouseEnter += (s, e) =>
            {
                container.RenderTransform = new ScaleTransform(1.1, 1.1);
                Panel.SetZIndex(container, 1000);
            };

            container.MouseLeave += (s, e) =>
            {
                container.RenderTransform = new ScaleTransform(1.0, 1.0);
                Panel.SetZIndex(container, 1);
            };

            container.MouseLeftButtonDown += (s, e) =>
            {
                ShowSiteDetails(site);
                e.Handled = true;
            };

            container.ToolTip = site.ToolTipText;

            CountyMapCanvas.Children.Add(container);
            _siteMarkers[site.Id] = container;

            PositionSiteMarker(site);
        }

        private Rectangle CreateStatusSquare(Brush fill, string tooltip)
        {
            return new Rectangle
            {
                Width = 16,
                Height = 16,
                Fill = fill,
                Stroke = Brushes.DarkGray,
                StrokeThickness = 0.5,
                Margin = new Thickness(1),
                ToolTip = tooltip
            };
        }

        private void PositionSiteMarker(SiteMapViewModel site)
        {
            if (_siteMarkers.TryGetValue(site.Id, out var marker))
            {
                Canvas.SetLeft(marker, site.MapX - marker.Width / 2);
                Canvas.SetTop(marker, site.MapY - marker.Height / 2);
            }
        }

        private void UpdateSiteMarker(SiteMapViewModel site)
        {
            if (_siteMarkers.TryGetValue(site.Id, out var oldMarker))
            {
                CountyMapCanvas.Children.Remove(oldMarker);
                _siteMarkers.Remove(site.Id);
            }

            CreateSiteMarker(site);
        }
        #endregion

        #region Site Details
        private void ShowSiteDetails(SiteMapViewModel site)
        {
            var detailWindow = new Window
            {
                Title = $"Site Details - {site.Name}",
                Width = 400,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new TextBlock
            {
                Text = site.Name,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var detailsPanel = new StackPanel();
            Grid.SetRow(detailsPanel, 1);

            detailsPanel.Children.Add(new TextBlock { Text = $"County: {site.County}", Margin = new Thickness(0, 2, 0, 2) });
            detailsPanel.Children.Add(new TextBlock
            {
                Text = $"Status: {site.Status}",
                Margin = new Thickness(0, 2, 0, 2),
                FontWeight = FontWeights.Bold,
                Foreground = site.FillBrush
            });

            detailsPanel.Children.Add(new Separator { Margin = new Thickness(0, 5, 0, 5) });

            var normalCount = site.TotalSensors - site.AlarmSensors;
            detailsPanel.Children.Add(new TextBlock { Text = $"Total Sensors: {site.TotalSensors}", Margin = new Thickness(0, 2, 0, 2) });
            detailsPanel.Children.Add(new TextBlock { Text = $"Normal Sensors: {normalCount}", Margin = new Thickness(0, 2, 0, 2), Foreground = Brushes.Green });
            detailsPanel.Children.Add(new TextBlock { Text = $"Alarm Sensors: {site.AlarmSensors}", Margin = new Thickness(0, 2, 0, 2), Foreground = Brushes.Orange });

            detailsPanel.Children.Add(new Separator { Margin = new Thickness(0, 5, 0, 5) });

            detailsPanel.Children.Add(new TextBlock { Text = $"Coordinates: {site.Latitude:F4}, {site.Longitude:F4}", Margin = new Thickness(0, 2, 0, 2) });
            detailsPanel.Children.Add(new TextBlock { Text = $"Map Position: X={site.MapX:F0}, Y={site.MapY:F0}", Margin = new Thickness(0, 2, 0, 2) });

            grid.Children.Add(detailsPanel);

            var closeButton = new Button
            {
                Content = "Close",
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            closeButton.Click += (s, e) => detailWindow.Close();
            Grid.SetRow(closeButton, 2);
            grid.Children.Add(closeButton);

            detailWindow.Content = grid;
            detailWindow.ShowDialog();
        }
        #endregion

        #region Statistics
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

        #region Zoom Functionality
        /// <summary>
        /// Handle mouse wheel events for zooming to cursor position
        /// </summary>
        private void CountyMapScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                e.Handled = true;

                // Get mouse position relative to the map container
                var mousePos = e.GetPosition(CountyMapViewbox);

                // Calculate new zoom level
                var oldZoom = _currentZoom;
                if (e.Delta > 0)
                {
                    _currentZoom = Math.Min(MAX_ZOOM, _currentZoom * ZOOM_FACTOR);
                }
                else
                {
                    _currentZoom = Math.Max(MIN_ZOOM, _currentZoom / ZOOM_FACTOR);
                }

                // Apply zoom transformation with focus point
                var group = new TransformGroup();

                // First, translate to center the zoom point
                group.Children.Add(new TranslateTransform(-mousePos.X, -mousePos.Y));

                // Then scale
                group.Children.Add(new ScaleTransform(_currentZoom, _currentZoom));

                // Finally, translate back
                group.Children.Add(new TranslateTransform(mousePos.X, mousePos.Y));

                CountyMapViewbox.RenderTransform = group;

                System.Diagnostics.Debug.WriteLine($"🔍 County Map Zoom level: {_currentZoom:F1}x at ({mousePos.X:F0}, {mousePos.Y:F0})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error handling mouse wheel: {ex.Message}");
            }
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = 1.0;

            // Clear any existing transforms and set to identity
            CountyMapViewbox.RenderTransform = Transform.Identity;

            // Force a layout update to ensure the reset takes effect immediately
            CountyMapViewbox.UpdateLayout();

            System.Diagnostics.Debug.WriteLine("🏠 County Map reset to original view");
        }
        #endregion

        #region Event Handlers
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_countyPath != null)
            {
                ScaleCountyToFit();
            }

            // Don't reposition sites on size change - they should maintain their relative positions
        }
        #endregion
    }
}