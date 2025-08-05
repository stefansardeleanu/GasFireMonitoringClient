// File: Views/Controls/RomaniaMapView.xaml.cs
// SVG Resource Version - Loads romania_map.svg from resources

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml;
using GasFireMonitoringClient.Models.ViewModels;

// Alias to resolve Path ambiguity
using WpfPath = System.Windows.Shapes.Path;

namespace GasFireMonitoringClient.Views.Controls
{
    /// <summary>
    /// Interactive Romania map using SVG resource
    /// </summary>
    public partial class RomaniaMapView : UserControl
    {
        #region Events
        /// <summary>
        /// Event fired when a county is clicked
        /// </summary>
        public event EventHandler<string>? CountyClicked;
        #endregion

        #region Private Fields
        private readonly ObservableCollection<CountyViewModel> _counties = new();
        private readonly Dictionary<string, WpfPath> _countyPaths = new();
        private readonly Dictionary<string, TextBlock> _countyLabels = new();
        private readonly Dictionary<string, Border> _countyStats = new();
        private ToolTip? _currentTooltip;

        // Zoom functionality
        private double _currentZoom = 1.0;
        private const double ZOOM_FACTOR = 1.2;
        private const double MIN_ZOOM = 0.5;
        private const double MAX_ZOOM = 3.0;

        // Auto-refresh timer
        private System.Windows.Threading.DispatcherTimer? _refreshTimer;
        private ObservableCollection<SiteViewModel>? _lastSiteData;
        #endregion

        #region Constructor
        public RomaniaMapView()
        {
            InitializeComponent();
            InitializeCountyData();
            LoadMap();
            SetupAutoRefresh();

            // Clean up timer when control is unloaded
            Unloaded += (s, e) => StopAutoRefresh();
        }
        #endregion

        #region Map Loading
        private void LoadMap()
        {
            try
            {
                UpdateDebugText("🗺️ Loading Romania map from resources...");

                // Clear existing elements but preserve counties data
                MapCanvas.Children.Clear();
                _countyPaths.Clear();
                _countyLabels.Clear();
                _countyStats.Clear();

                // Load SVG from embedded resource
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "GasFireMonitoringClient.Resources.Maps.romania_map.svg";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        UpdateDebugText("❌ Map resource not found!");
                        return;
                    }

                    var doc = new XmlDocument();
                    doc.Load(stream);

                    UpdateDebugText("✅ SVG loaded successfully");

                    // Process the SVG
                    ProcessSvgDocument(doc);
                }

                // Reapply site data if we have any
                if (_counties.Any(c => c.TotalSites > 0))
                {
                    foreach (var county in _counties)
                    {
                        UpdateCountyAppearance(county);
                    }
                    UpdateOverviewStatistics();
                }
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error loading map: {ex.Message}");
            }
        }

        private void ProcessSvgDocument(XmlDocument doc)
        {
            try
            {
                // Set up the canvas size based on SVG viewBox
                var svg = doc.DocumentElement;
                if (svg?.GetAttribute("viewBox") is string viewBox)
                {
                    var parts = viewBox.Split(' ');
                    if (parts.Length == 4)
                    {
                        MapCanvas.Width = double.Parse(parts[2]);
                        MapCanvas.Height = double.Parse(parts[3]);
                    }
                }

                // Find the layer with county paths
                var countyPaths = doc.GetElementsByTagName("path");
                var pathCount = 0;

                foreach (XmlNode node in countyPaths)
                {
                    // Skip non-county paths
                    var id = node.Attributes?["id"]?.Value;
                    if (string.IsNullOrEmpty(id) || !id.StartsWith("RO"))
                        continue;

                    ProcessCountyElement(node, MapCanvas);
                    pathCount++;
                }

                UpdateDebugText($"✅ Loaded {pathCount} counties");

                // Fit the map to the viewbox
                if (MapViewbox != null)
                {
                    MapViewbox.Stretch = Stretch.Uniform;
                }
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error processing SVG: {ex.Message}");
            }
        }

        private void ProcessCountyElement(XmlNode node, Canvas targetCanvas)
        {
            try
            {
                var pathData = node.Attributes?["d"]?.Value;
                if (string.IsNullOrEmpty(pathData)) return;

                var id = node.Attributes?["id"]?.Value ?? "";
                var name = node.Attributes?["name"]?.Value ?? "";
                var countyName = GetCountyName(id, name);

                // Create the path
                var path = new WpfPath
                {
                    Data = Geometry.Parse(pathData),
                    Fill = Brushes.LightGreen,
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 1.5,
                    Tag = countyName,
                    Cursor = Cursors.Hand
                };

                // Add hover effect
                path.MouseEnter += (s, e) =>
                {
                    if (_counties.FirstOrDefault(c => c.Name == countyName) is CountyViewModel county)
                    {
                        ShowCountyTooltip(county);
                    }
                };

                path.MouseLeave += (s, e) =>
                {
                    HideCountyTooltip();
                };

                path.MouseLeftButtonDown += (s, e) =>
                {
                    CountyClicked?.Invoke(this, countyName);
                    e.Handled = true;
                };

                targetCanvas.Children.Add(path);
                _countyPaths[countyName] = path;

                // Find or create county view model
                var countyVm = _counties.FirstOrDefault(c => c.Name == countyName);
                if (countyVm == null)
                {
                    countyVm = new CountyViewModel { Name = countyName };
                    _counties.Add(countyVm);
                }

                // Get center point for labels
                var center = GetCountyCenter(path);

                // Extract and display county code
                var countyCode = ExtractCountyCode(id);
                if (!string.IsNullOrEmpty(countyCode))
                {
                    CreateCountyLabel(id, countyCode, center);
                }

                // Create statistics display
                CreateCountyStats(id, countyVm, center);

                UpdateDebugText($"✅ Loaded county: {countyName} (Code: {countyCode})");
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error processing county: {ex.Message}");
            }
        }
        #endregion

        #region County Display Methods
        /// <summary>
        /// Extract county code from ID (e.g., "ROBC" -> "BC")
        /// </summary>
        private string ExtractCountyCode(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 4)
                return "";

            // Remove "RO" prefix to get county code
            if (id.StartsWith("RO"))
            {
                return id.Substring(2);
            }

            return id;
        }

        /// <summary>
        /// Create text label for county code
        /// </summary>
        private void CreateCountyLabel(string countyId, string countyCode, Point center)
        {
            var label = new TextBlock
            {
                Text = countyCode,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false // Don't interfere with mouse events
            };

            // Position the label at the county center
            Canvas.SetLeft(label, center.X - 10); // Adjust for text width
            Canvas.SetTop(label, center.Y - 8);   // Adjust for text height
            Canvas.SetZIndex(label, 10); // Ensure labels are on top

            // Add to the canvas
            MapCanvas.Children.Add(label);

            // Store reference for updates
            if (!_countyLabels.ContainsKey(countyId))
            {
                _countyLabels[countyId] = label;
            }
        }

        /// <summary>
        /// Create statistics display for a county
        /// </summary>
        private void CreateCountyStats(string countyId, CountyViewModel county, Point center)
        {
            // Container for the statistics
            var statsContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)), // Semi-transparent white
                Margin = new Thickness(2),
                IsHitTestVisible = false // Don't interfere with mouse events
            };

            // Create three colored squares with counters
            var normalSquare = CreateStatSquare(Brushes.Green, county.NormalSites.ToString(), "Normal");
            var alarmSquare = CreateStatSquare(Brushes.Orange, county.AlarmSites.ToString(), "Alarms");
            var errorSquare = CreateStatSquare(Brushes.Red, county.ErrorSites.ToString(), "Errors");

            statsContainer.Children.Add(normalSquare);
            statsContainer.Children.Add(alarmSquare);
            statsContainer.Children.Add(errorSquare);

            // Create border for better visibility
            var border = new Border
            {
                Child = statsContainer,
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                IsHitTestVisible = false // Don't interfere with mouse events
            };

            // Position below the county code
            Canvas.SetLeft(border, center.X - 30); // Center the stats
            Canvas.SetTop(border, center.Y + 10);  // Below the county code
            Canvas.SetZIndex(border, 10); // Ensure stats are on top

            // Add to canvas
            MapCanvas.Children.Add(border);

            // Store reference
            if (!_countyStats.ContainsKey(countyId))
            {
                _countyStats[countyId] = border;
            }
        }

        /// <summary>
        /// Create a single statistics square
        /// </summary>
        private Border CreateStatSquare(Brush color, string count, string tooltip)
        {
            var grid = new Grid
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(1)
            };

            // Colored background
            var rect = new Rectangle
            {
                Fill = color,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5
            };

            // Count text
            var text = new TextBlock
            {
                Text = count,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            grid.Children.Add(rect);
            grid.Children.Add(text);

            var border = new Border
            {
                Child = grid,
                ToolTip = $"{tooltip}: {count}"
            };

            return border;
        }

        /// <summary>
        /// Get the center point of a county path
        /// </summary>
        private Point GetCountyCenter(WpfPath countyPath)
        {
            var geometry = countyPath.Data;
            var bounds = geometry.Bounds;

            // Return the center of the bounding box
            return new Point(
                bounds.X + bounds.Width / 2,
                bounds.Y + bounds.Height / 2
            );
        }

        /// <summary>
        /// Update the statistics display for a county
        /// </summary>
        private void UpdateCountyStatsDisplay(CountyViewModel county)
        {
            // Find the county ID from name
            string countyId = GetCountyIdFromName(county.Name);

            if (string.IsNullOrEmpty(countyId) || !_countyStats.ContainsKey(countyId))
                return;

            var statsContainer = _countyStats[countyId];
            if (statsContainer.Child is StackPanel panel && panel.Children.Count >= 3)
            {
                // Update Normal square
                if (panel.Children[0] is Border normalBorder &&
                    normalBorder.Child is Grid normalGrid &&
                    normalGrid.Children.Count > 1 &&
                    normalGrid.Children[1] is TextBlock normalText)
                {
                    normalText.Text = county.NormalSites.ToString();
                    normalBorder.ToolTip = $"Normal: {county.NormalSites}";
                }

                // Update Alarm square
                if (panel.Children[1] is Border alarmBorder &&
                    alarmBorder.Child is Grid alarmGrid &&
                    alarmGrid.Children.Count > 1 &&
                    alarmGrid.Children[1] is TextBlock alarmText)
                {
                    alarmText.Text = county.AlarmSites.ToString();
                    alarmBorder.ToolTip = $"Alarms: {county.AlarmSites}";
                }

                // Update Error square
                if (panel.Children[2] is Border errorBorder &&
                    errorBorder.Child is Grid errorGrid &&
                    errorGrid.Children.Count > 1 &&
                    errorGrid.Children[1] is TextBlock errorText)
                {
                    errorText.Text = county.ErrorSites.ToString();
                    errorBorder.ToolTip = $"Errors: {county.ErrorSites}";
                }
            }
        }
        #endregion

        #region Zoom Functionality
        /// <summary>
        /// Handle mouse wheel events for zooming to cursor position
        /// </summary>
        private void MapScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                e.Handled = true;

                // Get mouse position relative to the map container
                var mousePos = e.GetPosition(MapViewbox);

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

                MapViewbox.RenderTransform = group;

                UpdateDebugText($"🔍 Zoom level: {_currentZoom:F1}x at ({mousePos.X:F0}, {mousePos.Y:F0})");
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error handling mouse wheel: {ex.Message}");
            }
        }



        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = 1.0;
            MapViewbox.RenderTransform = new ScaleTransform(1.0, 1.0);

            // Center the map
            MapScrollViewer.ScrollToHorizontalOffset((MapScrollViewer.ExtentWidth - MapScrollViewer.ViewportWidth) / 2);
            MapScrollViewer.ScrollToVerticalOffset((MapScrollViewer.ExtentHeight - MapScrollViewer.ViewportHeight) / 2);

            UpdateDebugText("🏠 Zoom reset to 1.0x");
        }
        #endregion

        #region Tooltip Management
        private void ShowCountyTooltip(CountyViewModel county)
        {
            _currentTooltip = new ToolTip
            {
                Content = county.ToolTipText,
                IsOpen = true
            };
        }

        private void HideCountyTooltip()
        {
            if (_currentTooltip != null)
            {
                _currentTooltip.IsOpen = false;
                _currentTooltip = null;
            }
        }
        #endregion

        #region Helper Methods
        private void UpdateDebugText(string message)
        {
            if (DebugText != null)
            {
                DebugText.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
            }
        }

        private void UpdateOverviewStatistics()
        {
            int totalCounties = _counties.Count;
            int normalCounties = _counties.Count(c => c.Status == "normal");
            int alarmCounties = _counties.Count(c => c.Status == "alarm");
            int errorCounties = _counties.Count(c => c.Status == "error");
            int totalSites = _counties.Sum(c => c.TotalSites);

            TotalCountiesText.Text = totalCounties.ToString();
            NormalCountiesText.Text = normalCounties.ToString();
            AlarmCountiesText.Text = alarmCounties.ToString();
            ErrorCountiesText.Text = errorCounties.ToString();
            TotalSitesText.Text = totalSites.ToString();
        }

        /// <summary>
        /// Convert SVG element ID to readable county name
        /// </summary>
        private string GetCountyName(string id, string name)
        {
            // Use the name attribute if available, otherwise convert ID
            if (!string.IsNullOrEmpty(name))
                return name;

            // Convert common IDs to county names
            return id switch
            {
                "ROSM" => "Satu Mare",
                "ROAR" => "Arad",
                "ROBH" => "Bihor",
                "ROTM" => "Timiș",
                "ROMH" => "Mehedinți",
                "RODJ" => "Dolj",
                "ROCL" => "Călărași",
                "ROTR" => "Teleorman",
                "ROGR" => "Giurgiu",
                "ROCT" => "Constanța",
                "ROOT" => "Olt",
                "ROCS" => "Caraș-Severin",
                "ROBT" => "Botoșani",
                "ROIS" => "Iași",
                "ROVS" => "Vaslui",
                "ROGL" => "Galați",
                "ROSV" => "Suceava",
                "ROMM" => "Maramureș",
                "ROTL" => "Tulcea",
                "ROCJ" => "Cluj",
                "ROBN" => "Bistrița-Năsăud",
                "ROSJ" => "Sălaj",
                "RODB" => "Dâmbovița",
                "ROIF" => "Ilfov",
                "ROAG" => "Argeș",
                "ROGJ" => "Gorj",
                "ROHD" => "Hunedoara",
                "ROVL" => "Vâlcea",
                "ROPH" => "Prahova",
                "ROCV" => "Covasna",
                "ROVN" => "Vrancea",
                "ROBZ" => "Buzău",
                "ROBV" => "Brașov",
                "ROSB" => "Sibiu",
                "ROMS" => "Mureș",
                "ROHR" => "Harghita",
                "RONT" => "Neamț",
                "ROBC" => "Bacău",
                "ROAB" => "Alba",
                "ROBR" => "Brăila",
                "ROIL" => "Ialomița",
                "ROB" => "București",
                _ => id // Default to ID if no mapping found
            };
        }

        /// <summary>
        /// Helper method to get county ID from name
        /// </summary>
        private string GetCountyIdFromName(string countyName)
        {
            // Reverse mapping of common county names to IDs
            return countyName switch
            {
                "Prahova" => "ROPH",
                "Gorj" => "ROGJ",
                "Bacău" => "ROBC",
                "București" => "ROB",
                "Alba" => "ROAB",
                "Arad" => "ROAR",
                "Argeș" => "ROAG",
                "Bihor" => "ROBH",
                "Bistrița-Năsăud" => "ROBN",
                "Botoșani" => "ROBT",
                "Brăila" => "ROBR",
                "Brașov" => "ROBV",
                "Buzău" => "ROBZ",
                "Călărași" => "ROCL",
                "Caraș-Severin" => "ROCS",
                "Cluj" => "ROCJ",
                "Constanța" => "ROCT",
                "Covasna" => "ROCV",
                "Dâmbovița" => "RODB",
                "Dolj" => "RODJ",
                "Galați" => "ROGL",
                "Giurgiu" => "ROGR",
                "Harghita" => "ROHR",
                "Hunedoara" => "ROHD",
                "Ialomița" => "ROIL",
                "Iași" => "ROIS",
                "Ilfov" => "ROIF",
                "Maramureș" => "ROMM",
                "Mehedinți" => "ROMH",
                "Mureș" => "ROMS",
                "Neamț" => "RONT",
                "Olt" => "ROOT",
                "Satu Mare" => "ROSM",
                "Sălaj" => "ROSJ",
                "Sibiu" => "ROSB",
                "Suceava" => "ROSV",
                "Teleorman" => "ROTR",
                "Timiș" => "ROTM",
                "Tulcea" => "ROTL",
                "Vâlcea" => "ROVL",
                "Vaslui" => "ROVS",
                "Vrancea" => "ROVN",
                _ => ""
            };
        }
        #endregion

        #region Auto-Refresh
        /// <summary>
        /// Setup auto-refresh timer
        /// </summary>
        private void SetupAutoRefresh()
        {
            _refreshTimer = new System.Windows.Threading.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();

            UpdateDebugText("⏱️ Auto-refresh enabled (5 seconds)");
        }

        /// <summary>
        /// Stop auto-refresh timer
        /// </summary>
        private void StopAutoRefresh()
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
            UpdateDebugText("⏹️ Auto-refresh stopped");
        }

        /// <summary>
        /// Timer tick event - refresh the map data
        /// </summary>
        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Only refresh if we have site data
                if (_lastSiteData != null && _lastSiteData.Any())
                {
                    // Update each county based on sites in that county
                    foreach (var county in _counties)
                    {
                        county.UpdateFromSites(_lastSiteData);
                        UpdateCountyAppearance(county);
                    }

                    UpdateOverviewStatistics();

                    // Update last refresh time
                    if (LastRefreshText != null)
                    {
                        LastRefreshText.Text = DateTime.Now.ToString("HH:mm:ss");
                    }

                    UpdateDebugText($"🔄 Auto-refresh completed at {DateTime.Now:HH:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error during auto-refresh: {ex.Message}");
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Update county status from site data
        /// </summary>
        public void UpdateCountyStatus(ObservableCollection<SiteViewModel> sites)
        {
            try
            {
                UpdateDebugText($"Updating status for {sites?.Count ?? 0} sites");

                // Store the site data for auto-refresh
                _lastSiteData = sites;

                if (sites == null || !sites.Any())
                {
                    UpdateDebugText("No sites to display");
                    return;
                }

                // Update each county based on sites in that county
                foreach (var county in _counties)
                {
                    county.UpdateFromSites(sites);
                    UpdateCountyAppearance(county);
                }

                UpdateOverviewStatistics();
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error updating county status: {ex.Message}");
            }
        }
        #endregion

        #region Private Methods
        private void InitializeCountyData()
        {
            // This will be populated as counties are loaded from SVG
            _counties.Clear();
        }

        private void UpdateCountyAppearance(CountyViewModel county)
        {
            try
            {
                // Don't update the path fill color - keep it as default light green
                // Only update the statistics display
                UpdateCountyStatsDisplay(county);
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error updating county appearance: {ex.Message}");
            }
        }
        #endregion
    }
}