using System;
using System.Linq;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

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
                Text = "ScreenShift"
            };
            
            _trayIcon.Click += TrayIcon_Click;
            _trayIcon.DoubleClick += TrayIcon_Click;
        }

        private System.Drawing.Icon CreateTrayIcon()
        {
            // Create a high-quality tray icon (32x32 for high DPI)
            using var bitmap = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bitmap);
            
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            // Colors
            var whitePen = new System.Drawing.Pen(System.Drawing.Color.White, 2);
            var greenBrush = new SolidBrush(System.Drawing.Color.FromArgb(12, 172, 92)); // AditiKraft Green
            var whiteBrush = new SolidBrush(System.Drawing.Color.White);

            // 1. Draw Monitor Screen (Green filled)
            // Fill the screen area
            g.FillRectangle(greenBrush, 4, 5, 24, 15);
            
            // 2. Draw Monitor Bezel (White outline)
            // Draw outside rectangle
            g.DrawRectangle(whitePen, 2, 3, 28, 19);
            
            // 3. Draw Stand (White)
            // Neck
            g.FillRectangle(whiteBrush, 14, 23, 4, 3);
            // Base
            g.FillRectangle(whiteBrush, 8, 26, 16, 2);

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
            var disabledMonitors = Monitors.Where(m => !m.IsEnabled).ToList();

            if (activeMonitors.Count == 0 && disabledMonitors.Count == 0) return;

            // Get visual dimensions (accounting for rotation)
            int GetVisualWidth(MonitorInfo m) => m.Orientation.Contains("Portrait") ? m.Height : m.Width;
            int GetVisualHeight(MonitorInfo m) => m.Orientation.Contains("Portrait") ? m.Width : m.Height;

            // Calculate layout for active monitors
            int minX = 0, minY = 0, maxX = 0, maxY = 0;
            
            if (activeMonitors.Count > 0)
            {
                minX = activeMonitors.Min(m => m.PositionX);
                minY = activeMonitors.Min(m => m.PositionY);
                maxX = activeMonitors.Max(m => m.PositionX + GetVisualWidth(m));
                maxY = activeMonitors.Max(m => m.PositionY + GetVisualHeight(m));
            }
            
            // assign positions to disabled monitors (stack them to the right)
            if (disabledMonitors.Count > 0)
            {
                int startX = activeMonitors.Count > 0 ? maxX + 100 : 0;
                int startY = activeMonitors.Count > 0 ? activeMonitors.Min(m => m.PositionY) : 0;

                foreach (var dm in disabledMonitors)
                {
                    // Use a temporary position for visualization only
                    dm.PositionX = startX;
                    dm.PositionY = startY;
                    
                    int w = GetVisualWidth(dm);
                    int h = GetVisualHeight(dm);
                    
                    startX += w + 50;
                }
                
                // Recalculate bounds including disabled monitors
                var allMonitors = activeMonitors.Concat(disabledMonitors).ToList();
                minX = allMonitors.Min(m => m.PositionX);
                minY = allMonitors.Min(m => m.PositionY);
                maxX = allMonitors.Max(m => m.PositionX + GetVisualWidth(m));
                maxY = allMonitors.Max(m => m.PositionY + GetVisualHeight(m));
            }

            var monitorsToDraw = activeMonitors.Concat(disabledMonitors).ToList();

            double totalWidth = maxX - minX;
            double totalHeight = maxY - minY;
            
            if (totalWidth == 0) totalWidth = 1920; 
            if (totalHeight == 0) totalHeight = 1080;

            double canvasWidth = MonitorCanvas.Width;
            double canvasHeight = MonitorCanvas.Height;

            double scaleX = canvasWidth / totalWidth;
            double scaleY = canvasHeight / totalHeight;
            double scale = Math.Min(scaleX, scaleY) * 0.9; // 90% to leave padding
            
            // Limit scale if it's too huge (single small monitor)
            if (scale > 0.15) scale = 0.15; 

            double visualTotalWidth = totalWidth * scale;
            double visualTotalHeight = totalHeight * scale;
            double offsetX = (canvasWidth - visualTotalWidth) / 2;
            double offsetY = (canvasHeight - visualTotalHeight) / 2;

            foreach (var monitor in monitorsToDraw)
            {
                double x = ((monitor.PositionX - minX) * scale) + offsetX;
                double y = ((monitor.PositionY - minY) * scale) + offsetY;
                double width = GetVisualWidth(monitor) * scale;
                double height = GetVisualHeight(monitor) * scale;
                
                // Ensure min size for visibility
                if (width < 30) width = 30;
                if (height < 30) height = 30;

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
                if (!monitor.IsEnabled)
                {
                    // Disabled styling
                    border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 128, 128, 128)); // Very faint grey
                    border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 100, 100, 100)); // Grey border
                }
                else if (monitor.IsPrimary)
                {
                    border.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
                    border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 128, 128, 128));
                }
                else
                {
                    border.SetResourceReference(Border.BackgroundProperty, "MonitorIconBackgroundBrush");
                    border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 128, 128, 128));
                }
                
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
                
                if (monitor.IsEnabled)
                {
                    numberText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                    if (monitor.IsPrimary) numberText.Foreground = System.Windows.Media.Brushes.White;
                }
                else
                {
                    numberText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 128, 128, 128)); // Dim number
                }

                stack.Children.Add(numberText);

                if (width > 60 && height > 40)
                {
                    var nameText = new TextBlock
                    {
                        Text = monitor.IsEnabled ? monitor.DeviceName : "Disabled",
                        FontSize = 8,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = width - 8
                    };
                    
                    if (!monitor.IsEnabled)
                    {
                        nameText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 128, 128, 128));
                    }
                    else if (monitor.IsPrimary)
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
                
                if (monitor.IsEnabled)
                {
                    border.MouseLeftButtonDown += VisualMonitor_Click;
                }
                else
                {
                   // Maybe show a tooltip saying it's disabled?
                   border.ToolTip = "This monitor is currently disabled in Windows Settings.";
                }

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
            if (sender is System.Windows.Controls.Button btn)
            {
                // Create a fresh ContextMenu each time to pick up current theme resources
                var contextMenu = CreateThemeContextMenu();
                contextMenu.PlacementTarget = btn;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                contextMenu.IsOpen = true;
            }
        }

        private System.Windows.Controls.ContextMenu CreateThemeContextMenu()
        {
            var contextMenu = new System.Windows.Controls.ContextMenu();
            
            // Apply style from resources
            if (System.Windows.Application.Current.TryFindResource("ModernContextMenuStyle") is Style contextMenuStyle)
            {
                contextMenu.Style = contextMenuStyle;
            }

            var themes = new[] { ("System Default", "System"), ("Light", "Light"), ("Dark", "Dark") };
            
            foreach (var (header, tag) in themes)
            {
                var menuItem = new System.Windows.Controls.MenuItem
                {
                    Header = header,
                    Tag = tag,
                    IsCheckable = true,
                    IsChecked = tag == _currentTheme
                };
                
                // Apply style from resources
                if (System.Windows.Application.Current.TryFindResource("ModernMenuItemStyle") is Style menuItemStyle)
                {
                    menuItem.Style = menuItemStyle;
                }
                
                menuItem.Click += ThemeMenuItem_Click;
                contextMenu.Items.Add(menuItem);
            }

            return contextMenu;
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem item && item.Tag is string theme)
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

            var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
            dictionaries.Clear();
            
            string themeFile = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
            dictionaries.Add(new ResourceDictionary { Source = new Uri(themeFile, UriKind.Relative) });
            
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
