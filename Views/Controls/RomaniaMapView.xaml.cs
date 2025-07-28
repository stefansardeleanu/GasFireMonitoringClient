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
        private ObservableCollection<CountyViewModel> _counties = new();
        private Dictionary<string, WpfPath> _countyPaths = new();
        private Dictionary<string, Brush> _originalBrushes = new();
        private double _currentZoom = 1.0;
        #endregion

        #region Constructor
        public RomaniaMapView()
        {
            InitializeComponent();
            LoadSvgMap();
            InitializeCountyData();
        }
        #endregion

        #region SVG Loading
        /// <summary>
        /// Load the SVG map from resources and convert to WPF elements
        /// </summary>
        private async void LoadSvgMap()
        {
            try
            {
                UpdateDebugText("Loading SVG from resources...");

                // Load SVG from embedded resource
                var svgContent = LoadSvgFromResources();

                if (string.IsNullOrEmpty(svgContent))
                {
                    UpdateDebugText("❌ Failed to load SVG content");
                    return;
                }

                UpdateDebugText("✅ SVG loaded, parsing counties...");

                // Parse the SVG and create WPF elements
                await ParseSvgAndCreateElements(svgContent);

                // Hide loading text
                LoadingText.Visibility = Visibility.Collapsed;

                UpdateDebugText($"✅ Map loaded with {_countyPaths.Count} counties");
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error loading SVG: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error loading SVG: {ex}");
            }
        }

        /// <summary>
        /// Load SVG content from embedded resources
        /// </summary>
        private string LoadSvgFromResources()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "GasFireMonitoringClient.Resources.Maps.romania_map.svg";

                // Try to find the resource
                var resourceNames = assembly.GetManifestResourceNames();
                var actualResourceName = resourceNames.FirstOrDefault(r => r.Contains("romania_map.svg"));

                if (actualResourceName == null)
                {
                    UpdateDebugText($"❌ SVG resource not found. Available resources: {string.Join(", ", resourceNames.Take(3))}...");
                    return string.Empty;
                }

                using (var stream = assembly.GetManifestResourceStream(actualResourceName))
                {
                    if (stream == null)
                    {
                        UpdateDebugText("❌ Could not open resource stream");
                        return string.Empty;
                    }

                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error reading SVG resource: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Parse SVG content and create WPF Path elements
        /// </summary>
        private async System.Threading.Tasks.Task ParseSvgAndCreateElements(string svgContent)
        {
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(svgContent);

                // Create namespace manager for SVG
                var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                nsmgr.AddNamespace("svg", "http://www.w3.org/2000/svg");

                // Find all path elements with ID attributes, accounting for potential namespace
                var pathNodes = xmlDoc.SelectNodes("//svg:path[@id] | //path[@id]", nsmgr);

                if (pathNodes == null || pathNodes.Count == 0)
                {
                    // Debug the SVG content
                    UpdateDebugText($"❌ No path elements found. SVG structure: {xmlDoc.DocumentElement?.Name}");
                    return;
                }

                UpdateDebugText($"Found {pathNodes.Count} path elements");

                // Clear existing content
                SvgContainer.Children.Clear();

                int processedCount = 0;

                foreach (XmlNode pathNode in pathNodes)
                {
                    try
                    {
                        var pathElement = CreatePathFromXmlNode(pathNode);
                        if (pathElement != null)
                        {
                            SvgContainer.Children.Add(pathElement);
                            processedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing path node: {ex.Message}");
                    }
                }

                UpdateDebugText($"✅ Created {processedCount} county elements");

                // Debug the first path if none were processed
                if (processedCount == 0 && pathNodes.Count > 0)
                {
                    var firstPath = pathNodes[0];
                    UpdateDebugText($"Debug - First path: ID={firstPath.Attributes?["id"]?.Value}, Data={firstPath.Attributes?["d"]?.Value?.Substring(0, 50)}...");
                }
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error parsing SVG: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Create a WPF Path element from an XML node
        /// </summary>
        private WpfPath? CreatePathFromXmlNode(XmlNode pathNode)
        {
            try
            {
                var id = pathNode.Attributes?["id"]?.Value;
                var name = pathNode.Attributes?["name"]?.Value;
                var pathData = pathNode.Attributes?["d"]?.Value;

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pathData))
                {
                    return null;
                }

                // Convert county ID to readable name
                var countyName = ConvertIdToCountyName(id, name);

                var path = new WpfPath
                {
                    Data = Geometry.Parse(pathData),
                    Fill = Brushes.LightGray,
                    Stroke = Brushes.DarkGray,
                    StrokeThickness = 1,
                    Cursor = Cursors.Hand,
                    Tag = countyName,
                    ToolTip = $"{countyName} County - Click to view sites"
                };

                // Add event handlers
                path.MouseEnter += County_MouseEnter;
                path.MouseLeave += County_MouseLeave;
                path.MouseLeftButtonDown += County_Click;

                // Store the path for later reference
                _countyPaths[countyName] = path;
                _originalBrushes[countyName] = path.Fill;

                // Add county to the collection if not already present
                if (!_counties.Any(c => c.Name == countyName))
                {
                    _counties.Add(new CountyViewModel
                    {
                        Name = countyName,
                        Status = "offline",
                        TotalSites = 0
                    });
                }

                System.Diagnostics.Debug.WriteLine($"Created path for county: {countyName}");

                return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating path: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Convert SVG ID to readable county name
        /// </summary>
        private string ConvertIdToCountyName(string id, string? name)
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

                if (sites == null || !sites.Any())
                {
                    // Set test data for development
                    SetCountyStatus("Prahova", "normal", 8, 0);
                    SetCountyStatus("Gorj", "alarm", 2, 1);
                    UpdateOverviewStatistics();
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

        /// <summary>
        /// Set county status manually (for testing)
        /// </summary>
        public void SetCountyStatus(string countyName, string status, int totalSites = 0, int alarmSites = 0)
        {
            try
            {
                var county = _counties.FirstOrDefault(c => c.Name == countyName);
                if (county != null)
                {
                    county.Status = status;
                    county.TotalSites = totalSites;
                    county.AlarmSites = alarmSites;
                    county.NormalSites = totalSites - alarmSites;

                    UpdateCountyAppearance(county);
                    UpdateDebugText($"✅ Set {countyName}: {status} ({totalSites} sites)");
                }
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error setting county status: {ex.Message}");
            }
        }

        /// <summary>
        /// Test method to verify the map is working
        /// </summary>
        public void TestMapFunctionality()
        {
            UpdateDebugText("🧪 Testing map functionality...");

            // Set test data for counties where you have sites
            SetCountyStatus("Prahova", "normal", 8, 0);
            SetCountyStatus("Gorj", "alarm", 2, 1);

            UpdateDebugText("✅ Test data applied");
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
                if (_countyPaths.TryGetValue(county.Name, out var path))
                {
                    path.Fill = county.FillBrush;
                    path.Stroke = county.StrokeBrush;
                    path.StrokeThickness = county.StrokeThickness;
                    path.ToolTip = county.ToolTipText;

                    // Update original brush for hover effects
                    _originalBrushes[county.Name] = county.FillBrush;
                }
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error updating county appearance: {ex.Message}");
            }
        }

        private void UpdateOverviewStatistics()
        {
            try
            {
                var totalCounties = _countyPaths.Count;
                var activeCounties = _counties.Count(c => c.Status != "offline");
                var totalSites = _counties.Sum(c => c.TotalSites);
                var totalAlarms = _counties.Sum(c => c.AlarmSites);

                TotalCountiesText.Text = $"Counties: {totalCounties}";
                ActiveCountiesText.Text = $"Active: {activeCounties}";
                TotalSitesOverviewText.Text = $"Total Sites: {totalSites}";
                ActiveAlarmsOverviewText.Text = $"Active Alarms: {totalAlarms}";

                ActiveAlarmsOverviewText.Foreground = totalAlarms > 0 ? Brushes.Red : Brushes.Green;
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error updating statistics: {ex.Message}");
            }
        }

        private void UpdateDebugText(string message)
        {
            DebugText.Text = message;
            System.Diagnostics.Debug.WriteLine($"RomaniaMap: {message}");
        }
        #endregion

        #region Event Handlers
        private void County_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.Tag is string countyName)
                {
                    UpdateDebugText($"🎯 County clicked: {countyName}");
                    CountyClicked?.Invoke(this, countyName);
                }
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error handling county click: {ex.Message}");
            }
        }

        private void County_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                if (sender is WpfPath path && path.Tag is string countyName)
                {
                    // Make county brighter on hover
                    if (_originalBrushes.TryGetValue(countyName, out var originalBrush) &&
                        originalBrush is SolidColorBrush brush)
                    {
                        var color = brush.Color;
                        var brighterColor = Color.FromArgb(
                            color.A,
                            (byte)Math.Min(255, color.R + 40),
                            (byte)Math.Min(255, color.G + 40),
                            (byte)Math.Min(255, color.B + 40)
                        );
                        path.Fill = new SolidColorBrush(brighterColor);
                    }

                    path.StrokeThickness += 1;
                }
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error handling mouse enter: {ex.Message}");
            }
        }

        private void County_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                if (sender is WpfPath path && path.Tag is string countyName)
                {
                    // Restore original appearance
                    if (_originalBrushes.TryGetValue(countyName, out var originalBrush))
                    {
                        path.Fill = originalBrush;
                    }
                    path.StrokeThickness -= 1;
                }
            }
            catch (Exception ex)
            {
                UpdateDebugText($"❌ Error handling mouse leave: {ex.Message}");
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom *= 1.2;
            MapViewbox.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);
            UpdateDebugText($"🔍 Zoomed in: {_currentZoom:F1}x");
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom /= 1.2;
            MapViewbox.LayoutTransform = new ScaleTransform(_currentZoom, _currentZoom);
            UpdateDebugText($"🔍 Zoomed out: {_currentZoom:F1}x");
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = 1.0;
            MapViewbox.LayoutTransform = new ScaleTransform(1.0, 1.0);
            UpdateDebugText("🏠 Reset to original view");
        }
        #endregion
    }
}