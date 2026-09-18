using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace UninstallerPro
{
    public class Program
    {
        public const string RegistryKeyName = "OptiGuard";
        public const string ShortcutName = "OptiGuard.lnk";

        // Single source of truth is version.json -> injected into <Version> in
        // OptiGuard.csproj at build time -> read back here at runtime, so this
        // can never drift from what was actually built (was a hardcoded literal
        // duplicated in three places before).
        public static readonly string AppVersion =
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        // Same name as AppMutex in Setup.cs - lets the installer detect a running
        // OptiGuard and prompt to close it before overwriting files, instead of
        // failing partway through an update.
        public const string SingleInstanceMutexName = @"Global\OptiGuard-SingleInstance-4B8F1C6A";

        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--self-uninstall")
            {
                SelfUninstall();
                return 0;
            }

            // Headless entry point used by the Task Scheduler task registered
            // from Settings ("Scheduled automatic cleanup", see
            // ScheduledCleanupData.cs). No WPF Application/window is created -
            // this runs, cleans, writes a result marker, and exits, exactly
            // like a normal unattended scheduled task should behave.
            if (args.Length > 0 && args[0] == "--auto-clean")
            {
                try { I18n.CurrentLang = AppSettings.Load().Language; } catch { }
                bool createdForAutoClean;
                using (new Mutex(true, SingleInstanceMutexName, out createdForAutoClean))
                {
                    if (!createdForAutoClean)
                    {
                        // OptiGuard is already open (interactive) - don't run an
                        // unattended cleanup underneath the user while they're
                        // actively using the app.
                        Logger.Log("Scheduled auto-clean skipped: OptiGuard already running.");
                        return 0;
                    }
                    Logger.Log("Scheduled auto-clean started (headless).");
                    ScheduledCleanupData.RunHeadlessCleanup();
                }
                return 0;
            }

            bool createdNew;
            using (var singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Another OptiGuard is already running - don't open a second copy
                    // mid-scan/mid-cleanup on top of it.
                    return 0;
                }

                try { I18n.CurrentLang = AppSettings.Load().Language; } catch { }

                var app = new Application();
                app.DispatcherUnhandledException += (s, e) =>
                {
                    Logger.Log("Critical error (UI): " + e.Exception);
                    MessageBox.Show(I18n.T("app_name") + ":\n" + e.Exception.Message + "\n\n" + AppPaths.LogFile,
                        I18n.T("generic_error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Handled = true;
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    var ex = e.ExceptionObject as Exception;
                    Logger.Log("Critical error (unhandled): " + (ex != null ? ex.ToString() : e.ExceptionObject));
                    MessageBox.Show(I18n.T("app_name") + ":\n" + (ex != null ? ex.Message : "") + "\n\n" + AppPaths.LogFile,
                        I18n.T("generic_error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
                };

                try
                {
                    Logger.Log(I18n.T("app_name") + " started (native app)");

                    // Splash shows for a minimum of 800ms (even on a fast machine, so it
                    // never just flickers) while MainWindow is constructed, then hands off
                    // and closes itself - see SplashWindow.cs.
                    var splash = new SplashWindow();
                    splash.StatusText.Text = I18n.T("splash_status_loading");
                    splash.Show();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    System.Threading.Tasks.Task.Delay(800).ContinueWith(_ =>
                    {
                        splash.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                var win = new MainWindow();
                                app.MainWindow = win;
                                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                                win.Show();
                                splash.Close();
                            }
                            catch (Exception ex)
                            {
                                Logger.Log("Critical startup error: " + ex);
                                MessageBox.Show(I18n.T("app_name") + ":\n" + ex.Message, I18n.T("generic_error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
                                splash.Close();
                                app.Shutdown();
                            }
                        });
                    });

                    app.Run();
                }
                catch (Exception ex)
                {
                    Logger.Log("Critical startup error: " + ex);
                    MessageBox.Show(I18n.T("app_name") + ":\n" + ex.Message, I18n.T("generic_error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return 0;
            }
        }

        private static void SelfUninstall()
        {
            try
            {
                try { I18n.CurrentLang = AppSettings.Load().Language; } catch { }

                var installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

                var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName);
                if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

                var startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), ShortcutName);
                if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);

                try { Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + RegistryKeyName, false); } catch { }
                try { ScheduledCleanupData.Remove(); } catch { }

                // Single question, default = keep data (per the shared standard - not
                // "are you sure?" nagging, a genuine data-retention choice). Settings,
                // quarantine and logs live in %APPDATA%\UninstallerPro, entirely
                // separate from installDir, so they survive unless the user opts in
                // to removing them here.
                try
                {
                    var result = MessageBox.Show(
                        I18n.T("uninstall_delete_data_msg"),
                        I18n.T("uninstall_delete_data_title"),
                        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                    if (result == MessageBoxResult.Yes)
                    {
                        try { Directory.Delete(AppPaths.DataDir, true); } catch { }
                    }
                }
                catch { }

                // Schedules folder deletion after this process releases its file lock
                // (a running EXE cannot delete its own containing folder immediately).
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + installDir + "\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch { }
        }
    }
}
