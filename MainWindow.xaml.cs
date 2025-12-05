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
        private string _currentTheme = "System";

        public MainWindow()
        {
            InitializeComponent();
            SetupTrayIcon();
            Loaded += MainWindow_Loaded;
            
            // Listen for system theme changes
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            
            // Initialize theme
            UpdateThemeVisuals();
            
            // Hide window initially
            this.Hide();
        }

        private void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            // Just update visuals if we are in System mode. 
            if (_currentTheme == "System")
            {
                Dispatcher.Invoke(() => UpdateThemeVisuals());
            }
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
            CenterWindowOnCursor();

            // 3. Load data (refresh in background)
            await LoadMonitorsAsync();
        }

        private void CenterWindowOnCursor()
        {
            var workArea = SystemParameters.WorkArea;
            var cursorPos = System.Windows.Forms.Cursor.Position;
            
            // Horizontal: Center on cursor, but keep within screen bounds
            this.Left = Math.Min(cursorPos.X - (this.Width / 2), workArea.Right - this.Width - 10);
            this.Left = Math.Max(this.Left, workArea.Left + 10);
            
            UpdateVerticalPosition();
        }

        private void UpdateVerticalPosition()
        {
            var workArea = SystemParameters.WorkArea;
            var cursorPos = System.Windows.Forms.Cursor.Position;

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
            UpdateVerticalPosition();
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
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = monitor
                };
                
                // Set dynamic colors
                if (monitor.IsPrimary)
                {
                    border.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
                }
                else
                {
                    border.SetResourceReference(Border.BackgroundProperty, "MonitorIconBackgroundBrush");
                }
                
                // Border brush is always the same or maybe accent? Let's use a subtle border
                border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 128, 128, 128));

                // Add content
                var stack = new StackPanel
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var numberText = new TextBlock
                {
                    Text = monitor.DisplayNumber,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                };
                numberText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                
                // If primary, text should be white regardless of theme because background is blue
                if (monitor.IsPrimary)
                {
                    numberText.Foreground = System.Windows.Media.Brushes.White;
                }

                stack.Children.Add(numberText);

                if (width > 60 && height > 40)
                {
                    var nameText = new TextBlock
                    {
                        Text = monitor.DeviceName,
                        FontSize = 8,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = width - 8
                    };
                    
                    if (monitor.IsPrimary)
                    {
                        nameText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255));
                    }
                    else
                    {
                        nameText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                    }
                    
                    stack.Children.Add(nameText);
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

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
            {
                // Update checked state
                foreach (var item in btn.ContextMenu.Items)
                {
                    if (item is MenuItem menuItem && menuItem.Tag is string theme)
                    {
                        menuItem.IsChecked = theme == _currentTheme;
                    }
                }
                
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string theme)
            {
                ApplyTheme(theme);
            }
        }

        private void ApplyTheme(string theme)
        {
            _currentTheme = theme;
            UpdateThemeVisuals();
        }

        private void UpdateThemeVisuals()
        {
            bool isDark = true; // Default to dark

            if (_currentTheme == "System")
            {
                // Detect system theme
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    if (key != null)
                    {
                        object? val = key.GetValue("AppsUseLightTheme");
                        if (val is int i && i == 1)
                            isDark = false;
                    }
                }
                catch { }
            }
            else if (_currentTheme == "Light")
            {
                isDark = false;
            }
            // else isDark remains true (Dark mode)

            var resources = System.Windows.Application.Current.Resources;

            if (!isDark) // Light
            {
                resources["AppBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 243, 243));
                resources["SurfaceBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                resources["SurfaceHoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230));
                resources["SurfacePressedBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 210, 210));
                resources["BorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200));
                resources["TextPrimaryBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0));
                resources["TextSecondaryBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 100));
                resources["MonitorIconBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220));
            }
            else // Dark
            {
                resources["AppBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 31, 31));
                resources["SurfaceBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45));
                resources["SurfaceHoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 61, 61));
                resources["SurfacePressedBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(77, 77, 77));
                resources["BorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(61, 61, 61));
                resources["TextPrimaryBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                resources["TextSecondaryBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
                resources["MonitorIconBackgroundBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 64, 64));
            }
            
            // Re-render visual layout to apply new colors
            UpdateVisualLayout();
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
