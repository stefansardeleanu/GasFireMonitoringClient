// File: Views/Controls/RomaniaMapView.xaml.cs
// Romania map with interactive counties

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
    /// Interactive Romania map showing county status
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
        private Brush _originalFill = Brushes.LightGray;
        #endregion

        #region Constructor
        public RomaniaMapView()
        {
            InitializeComponent();
            InitializeCountyData();
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
                // Update each county based on sites in that county
                foreach (var county in _counties)
                {
                    county.UpdateFromSites(sites);
                    UpdateCountyAppearance(county);
                }

                // Update overview statistics
                UpdateOverviewStatistics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating county status: {ex.Message}");
            }
        }

        /// <summary>
        /// Set county status manually (for testing)
        /// </summary>
        public void SetCountyStatus(string countyName, string status, int totalSites = 0, int alarmSites = 0)
        {
            var county = _counties.FirstOrDefault(c => c.Name == countyName);
            if (county != null)
            {
                county.Status = status;
                county.TotalSites = totalSites;
                county.AlarmSites = alarmSites;
                county.NormalSites = totalSites - alarmSites;

                UpdateCountyAppearance(county);
                UpdateOverviewStatistics();
            }
        }
        #endregion

        #region Private Methods
        private void InitializeCountyData()
        {
            // Initialize county view models
            var countyNames = new[]
            {
                "Prahova", "Gorj", "Alba", "Arad", "Argeș", "Bacău", "Bihor", "Brașov",
                "Cluj", "București"
                // Add more counties as needed
            };

            _counties.Clear();
            foreach (var name in countyNames)
            {
                _counties.Add(new CountyViewModel
                {
                    Name = name,
                    Status = "offline",
                    TotalSites = 0
                });
            }

            // Set initial test data for your counties
            SetCountyStatus("Prahova", "normal", 8, 0);
            SetCountyStatus("Gorj", "alarm", 2, 1);
        }

        private void UpdateCountyAppearance(CountyViewModel county)
        {
            // Find the corresponding UI element
            var countyElement = FindCountyElement(county.Name);
            if (countyElement != null)
            {
                // Update fill color
                if (countyElement is Shape shape)
                {
                    shape.Fill = county.FillBrush;
                    shape.Stroke = county.StrokeBrush;
                    shape.StrokeThickness = county.StrokeThickness;

                    // Update tooltip
                    shape.ToolTip = county.ToolTipText;
                }
            }
        }

        private FrameworkElement? FindCountyElement(string countyName)
        {
            // Find the UI element corresponding to the county
            return countyName switch
            {
                "Prahova" => PrahovaCounty,
                "Gorj" => GorjCounty,
                "Alba" => AlbaCounty,
                "Arad" => AradCounty,
                "Argeș" => ArgesCounty,
                "Bacău" => BacauCounty,
                "Bihor" => BihorCounty,
                "Brașov" => BrasovCounty,
                "Cluj" => ClujCounty,
                "București" => BucharestCounty,
                _ => null
            };
        }

        private void UpdateOverviewStatistics()
        {
            var totalCounties = _counties.Count;
            var activeCounties = _counties.Count(c => c.Status != "offline");
            var totalSites = _counties.Sum(c => c.TotalSites);
            var totalAlarms = _counties.Sum(c => c.AlarmSites);

            TotalCountiesText.Text = $"Counties: {totalCounties}";
            ActiveCountiesText.Text = $"Active: {activeCounties}";
            TotalSitesOverviewText.Text = $"Total Sites: {totalSites}";
            ActiveAlarmsOverviewText.Text = $"Active Alarms: {totalAlarms}";

            // Color the alarms text based on count
            ActiveAlarmsOverviewText.Foreground = totalAlarms > 0 ? Brushes.Red : Brushes.Green;
        }
        #endregion

        #region Event Handlers
        private void County_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string countyName)
            {
                // Fire the county clicked event
                CountyClicked?.Invoke(this, countyName);
            }
        }

        private void County_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Shape shape)
            {
                // Store original fill and make it brighter on hover
                _originalFill = shape.Fill;

                // Make the county brighter on hover
                if (shape.Fill is SolidColorBrush brush)
                {
                    var color = brush.Color;
                    var brighterColor = Color.FromArgb(
                        color.A,
                        (byte)Math.Min(255, color.R + 30),
                        (byte)Math.Min(255, color.G + 30),
                        (byte)Math.Min(255, color.B + 30)
                    );
                    shape.Fill = new SolidColorBrush(brighterColor);
                }

                // Increase stroke thickness for emphasis
                shape.StrokeThickness += 1;
            }
        }

        private void County_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Shape shape)
            {
                // Restore original appearance
                shape.Fill = _originalFill;
                shape.StrokeThickness -= 1;
            }
        }
        #endregion
    }
}