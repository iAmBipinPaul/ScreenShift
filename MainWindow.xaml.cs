using System;
using System.Linq;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace MonitorSwitcher
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<MonitorInfo> Monitors { get; set; } = new();
        private NotifyIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            SetupTrayIcon();
            Loaded += MainWindow_Loaded;
            
            // Hide window initially
            this.Hide();
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Visible = true,
                Text = "Monitor Switcher"
            };
            
            _trayIcon.Click += TrayIcon_Click;
            _trayIcon.DoubleClick += TrayIcon_Click;
        }

        private System.Drawing.Icon CreateTrayIcon()
        {
            // Create a simple monitor icon
            using var bitmap = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bitmap);
            
            // Draw monitor shape
            g.Clear(System.Drawing.Color.Transparent);
            using var brush = new SolidBrush(System.Drawing.Color.FromArgb(0, 120, 212));
            using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 2);
            
            // Monitor body
            g.FillRectangle(brush, 4, 4, 24, 18);
            g.DrawRectangle(pen, 4, 4, 24, 18);
            
            // Stand
            g.FillRectangle(brush, 12, 22, 8, 3);
            g.FillRectangle(brush, 8, 25, 16, 3);
            
            // Convert to icon
            IntPtr hicon = bitmap.GetHicon();
            return System.Drawing.Icon.FromHandle(hicon);
        }

        private void TrayIcon_Click(object? sender, EventArgs e)
        {
            if (this.IsVisible)
            {
                this.Hide();
            }
            else
            {
                ShowPopup();
            }
        }

        private async void ShowPopup()
        {
            // 1. Show window immediately
            // Only show loading overlay if we don't have data yet (first run)
            if (Monitors.Count == 0)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
            
            this.Show();
            this.Activate();

            // 2. Position near the taskbar (using current/default size)
            RepositionWindow();

            // 3. Load data (refresh in background)
            await LoadMonitorsAsync();
        }

        private void RepositionWindow()
        {
            var workArea = SystemParameters.WorkArea;
            var cursorPos = System.Windows.Forms.Cursor.Position;
            
            // Horizontal: Center on cursor, but keep within screen bounds
            this.Left = Math.Min(cursorPos.X - (this.Width / 2), workArea.Right - this.Width - 10);
            this.Left = Math.Max(this.Left, workArea.Left + 10);
            
            // Vertical: Anchor to bottom (or top)
            if (cursorPos.Y > workArea.Height / 2)
            {
                // Taskbar at bottom -> Window sits above taskbar
                this.Top = workArea.Bottom - this.ActualHeight - 10;
            }
            else
            {
                // Taskbar at top -> Window sits below taskbar
                this.Top = workArea.Top + 10;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // No-op
        }

        private async Task LoadMonitorsAsync()
        {
            if (Monitors.Count == 0)
                StatusText.Text = "Loading...";
            else
                StatusText.Text = "Refreshing...";
            
            // Run heavy lifting in background
            var displays = await Task.Run(() => DisplayHelper.GetAllDisplays());
            
            // Update UI - Create new collection to avoid flickering
            var newMonitors = new ObservableCollection<MonitorInfo>();
            foreach (var display in displays)
            {
                newMonitors.Add(display);
            }
            
            Monitors = newMonitors;
            MonitorList.ItemsSource = Monitors;
            
            StatusText.Text = $"{Monitors.Count(m => m.IsEnabled)} active";
            
            UpdateVisualLayout();
            
            // Hide loading overlay
            LoadingOverlay.Visibility = Visibility.Collapsed;
            
            // Force layout update and reposition since size likely changed
            this.UpdateLayout();
            RepositionWindow();
        }

        private void UpdateVisualLayout()
        {
            MonitorCanvas.Children.Clear();
            
            var activeMonitors = Monitors.Where(m => m.IsEnabled).ToList();
            if (activeMonitors.Count == 0) return;

            // Get visual dimensions (accounting for rotation)
            int GetVisualWidth(MonitorInfo m) => m.Orientation.Contains("Portrait") ? m.Height : m.Width;
            int GetVisualHeight(MonitorInfo m) => m.Orientation.Contains("Portrait") ? m.Width : m.Height;

            // Find bounds
            int minX = activeMonitors.Min(m => m.PositionX);
            int minY = activeMonitors.Min(m => m.PositionY);
            int maxX = activeMonitors.Max(m => m.PositionX + GetVisualWidth(m));
            int maxY = activeMonitors.Max(m => m.PositionY + GetVisualHeight(m));

            double totalWidth = maxX - minX;
            double totalHeight = maxY - minY;

            double canvasWidth = MonitorCanvas.Width;
            double canvasHeight = MonitorCanvas.Height;

            double scaleX = canvasWidth / totalWidth;
            double scaleY = canvasHeight / totalHeight;
            double scale = Math.Min(scaleX, scaleY) * 0.9; // 90% to leave padding

            double visualTotalWidth = totalWidth * scale;
            double visualTotalHeight = totalHeight * scale;
            double offsetX = (canvasWidth - visualTotalWidth) / 2;
            double offsetY = (canvasHeight - visualTotalHeight) / 2;

            foreach (var monitor in activeMonitors)
            {
                double x = ((monitor.PositionX - minX) * scale) + offsetX;
                double y = ((monitor.PositionY - minY) * scale) + offsetY;
                double width = GetVisualWidth(monitor) * scale;
                double height = GetVisualHeight(monitor) * scale;

                // Create monitor rectangle
                var border = new Border
                {
                    Width = width,
                    Height = height,
                    Background = monitor.IsPrimary 
                        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212))
                        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 64, 64)),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 100)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = monitor
                };

                // Add content
                var stack = new StackPanel
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                stack.Children.Add(new TextBlock
                {
                    Text = monitor.DisplayNumber,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                });

                if (width > 60 && height > 40)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = monitor.DeviceName,
                        FontSize = 8,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = width - 8
                    });
                }

                border.Child = stack;
                border.MouseLeftButtonDown += VisualMonitor_Click;

                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
                MonitorCanvas.Children.Add(border);
            }
        }

        private void VisualMonitor_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is MonitorInfo monitor)
            {
                SetPrimaryMonitor(monitor);
            }
        }

        private void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is MonitorInfo monitor)
            {
                SetPrimaryMonitor(monitor);
            }
        }

        private async void SetPrimaryMonitor(MonitorInfo monitor)
        {
            if (!monitor.IsEnabled)
            {
                StatusText.Text = "Monitor disabled";
                return;
            }

            if (monitor.IsPrimary)
            {
                StatusText.Text = "Already primary";
                return;
            }

            StatusText.Text = "Switching...";
            
            bool success = await Task.Run(() => DisplayHelper.SetPrimaryDisplay(monitor.DeviceKey));
            
            if (success)
            {
                StatusText.Text = "Done!";
                await Task.Delay(500);
                await LoadMonitorsAsync();
            }
            else
            {
                StatusText.Text = "Failed";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadMonitorsAsync();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            _trayIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Hide when clicking outside
            this.Hide();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Minimize to tray instead of closing
            e.Cancel = true;
            this.Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }

    // Value Converters
    public class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (Visibility)value == Visibility.Visible;
        }
    }

    public class InverseBoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (Visibility)value != Visibility.Visible;
        }
    }

    public class MonitorInfo : INotifyPropertyChanged
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
        public int PositionX { get; set; }
        public int PositionY { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
