using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace MonitorSwitcher
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<MonitorInfo> Monitors { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadMonitors();
        }

        private void LoadMonitors()
        {
            Monitors.Clear();
            var displays = DisplayHelper.GetAllDisplays();
            foreach (var display in displays)
            {
                Monitors.Add(display);
            }
            MonitorList.ItemsSource = Monitors;
            StatusText.Text = $"{Monitors.Count} monitor(s) detected";
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

    public class MonitorInfo
    {
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
    }
}
