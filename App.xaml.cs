using System;
using System.Threading;
using System.Windows;

namespace MonitorSwitcher
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private const string MutexName = "MonitorSwitcher_SingleInstance_Mutex";

        protected override void OnStartup(StartupEventArgs e)
        {
            // Try to create a mutex - if it already exists, another instance is running
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Show modern styled dialog
                var dialog = new AlreadyRunningDialog();
                dialog.ShowDialog();
                
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Release the mutex when the app exits
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            
            base.OnExit(e);
        }
    }
}
