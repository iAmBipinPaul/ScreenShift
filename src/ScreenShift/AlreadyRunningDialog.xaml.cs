using System.Windows;
using System.Windows.Input;

namespace ScreenShift
{
    public partial class AlreadyRunningDialog : Window
    {
        public AlreadyRunningDialog()
        {
            InitializeComponent();
            
            // Allow dragging the window
            MouseLeftButtonDown += (s, e) => DragMove();
            
            // Close on Escape key
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape || e.Key == Key.Enter)
                    Close();
            };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
