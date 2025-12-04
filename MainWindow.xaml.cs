using System;
using System.Linq;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;

namespace MonitorSwitcher
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<MonitorInfo> Monitors { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadMonitorsAsync();
        }

        private async Task LoadMonitorsAsync()
        {
            StatusText.Text = "Loading monitors...";
            
            Monitors.Clear();
            var displays = await Task.Run(() => DisplayHelper.GetAllDisplays());
            foreach (var display in displays)
            {
                Monitors.Add(display);
            }
            MonitorList.ItemsSource = Monitors;
            StatusText.Text = $"{Monitors.Count} monitor(s) detected";
            CalculateLayout();
        }

        private void LoadMonitors()
        {
            _ = LoadMonitorsAsync();
        }

        private void CalculateLayout()
        {
            if (Monitors.Count == 0) return;

            var activeMonitors = Monitors.Where(m => m.IsEnabled).ToList();
            if (activeMonitors.Count == 0) return;

            // Get visual dimensions (accounting for rotation)
            int GetVisualWidth(MonitorInfo m) => m.Orientation.Contains("Portrait") ? m.Height : m.Width;
            int GetVisualHeight(MonitorInfo m) => m.Orientation.Contains("Portrait") ? m.Width : m.Height;

            // Find bounds of the virtual desktop
            int minX = activeMonitors.Min(m => m.PositionX);
            int minY = activeMonitors.Min(m => m.PositionY);
            int maxX = activeMonitors.Max(m => m.PositionX + GetVisualWidth(m));
            int maxY = activeMonitors.Max(m => m.PositionY + GetVisualHeight(m));

            double totalWidth = maxX - minX;
            double totalHeight = maxY - minY;

            // Available drawing area (leave some padding)
            double canvasWidth = 600;
            double canvasHeight = 200;

            // Calculate scale to fit
            double scaleX = canvasWidth / totalWidth;
            double scaleY = canvasHeight / totalHeight;
            double scale = Math.Min(scaleX, scaleY);

            // Center the visualization
            double visualTotalWidth = totalWidth * scale;
            double visualTotalHeight = totalHeight * scale;
            double offsetX = (canvasWidth - visualTotalWidth) / 2;
            double offsetY = (canvasHeight - visualTotalHeight) / 2;

            foreach (var monitor in Monitors)
            {
                if (monitor.IsEnabled)
                {
                    monitor.VisualX = ((monitor.PositionX - minX) * scale) + offsetX;
                    monitor.VisualY = ((monitor.PositionY - minY) * scale) + offsetY;
                    
                    // Swap width/height for portrait orientation
                    if (monitor.Orientation.Contains("Portrait"))
                    {
                        monitor.VisualWidth = monitor.Height * scale;
                        monitor.VisualHeight = monitor.Width * scale;
                    }
                    else
                    {
                        monitor.VisualWidth = monitor.Width * scale;
                        monitor.VisualHeight = monitor.Height * scale;
                    }
                }
                else
                {
                    monitor.VisualWidth = 0;
                    monitor.VisualHeight = 0;
                }
            }
            
            // Force UI update
            MonitorList.ItemsSource = null;
            MonitorList.ItemsSource = Monitors;
        }

        private void MonitorCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is MonitorInfo monitor)
            {
                if (!monitor.IsEnabled)
                {
                    StatusText.Text = "Monitor is disabled";
                    return;
                }

                if (monitor.IsPrimary)
                {
                    StatusText.Text = "Already the primary monitor";
                    return;
                }

                StatusText.Text = $"Setting {monitor.DeviceName} as primary...";
                bool success = DisplayHelper.SetPrimaryDisplay(monitor.DeviceKey);
                
                if (success)
                {
                    StatusText.Text = $"{monitor.DeviceName} is now primary";
                    LoadMonitors();
                }
                else
                {
                    StatusText.Text = "Failed to change primary monitor";
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadMonitors();
        }



        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class MonitorInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string DeviceName { get; set; } = "";
        public string AdapterName { get; set; } = "";
        public string DeviceKey { get; set; } = "";
        public string DisplayNumber { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; }
        public string Orientation { get; set; } = "";
        public bool IsPrimary { get; set; }
        public bool IsEnabled { get; set; } = true;
        
        // Position data
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        
        // Visual properties for UI
        private double _visualX;
        public double VisualX 
        { 
            get => _visualX; 
            set { _visualX = value; OnPropertyChanged(nameof(VisualX)); } 
        }

        private double _visualY;
        public double VisualY 
        { 
            get => _visualY; 
            set { _visualY = value; OnPropertyChanged(nameof(VisualY)); } 
        }

        private double _visualWidth;
        public double VisualWidth 
        { 
            get => _visualWidth; 
            set { _visualWidth = value; OnPropertyChanged(nameof(VisualWidth)); } 
        }

        private double _visualHeight;
        public double VisualHeight 
        { 
            get => _visualHeight; 
            set { _visualHeight = value; OnPropertyChanged(nameof(VisualHeight)); } 
        }
    }
}
