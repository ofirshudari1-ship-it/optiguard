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
                var autoCleanSettings = AppSettings.Load();
                try { I18n.CurrentLang = autoCleanSettings.Language; } catch { }
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
                    ScheduledCleanupData.RunHeadlessCleanup(autoCleanSettings);
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
                        I18n.T("generic_error_title"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, Dialogs.RtlOptions());
                    e.Handled = true;
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    var ex = e.ExceptionObject as Exception;
                    Logger.Log("Critical error (unhandled): " + (ex != null ? ex.ToString() : e.ExceptionObject));
                    MessageBox.Show(I18n.T("app_name") + ":\n" + (ex != null ? ex.Message : "") + "\n\n" + AppPaths.LogFile,
                        I18n.T("generic_error_title"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, Dialogs.RtlOptions());
                };

                try
                {
                    Logger.Log(I18n.T("app_name") + " started (native app)");

                    // Splash timing/handoff per STANDARDS.md §19.2 (SplashWindow.cs owns
                    // only the visuals/animation):
                    //  - minimum 800ms on screen, enforced by measuring real elapsed time
                    //    against MainWindow construction and waiting out the difference -
                    //    not a blind fixed delay.
                    //  - an 8-second safety timeout so the splash can never hang forever
                    //    even if startup stalls.
                    //  - MainWindow is not shown until the moment splash closes, so there
                    //    is never a frame with both windows visible.
                    const int MinSplashDisplayMs = 800;
                    const int SafetyTimeoutMs = 8000;

                    var splashStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var splash = new SplashWindow();
                    splash.StatusText.Text = I18n.T("splash_status_loading");
                    splash.Show();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    bool handedOff = false;
                    var handoffLock = new object();

                    Action<MainWindow, Exception> finishStartup = (win, error) =>
                    {
                        lock (handoffLock)
                        {
                            if (handedOff) return;
                            handedOff = true;
                        }
                        try
                        {
                            if (error != null) throw error;
                            app.MainWindow = win;
                            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                            win.Show();
                            splash.Close();

                            // Persistent desktop widget (WidgetWindow.cs) - launches
                            // alongside the main window when enabled (default ON),
                            // per the "always visible" requirement. Uses the main
                            // window's already-loaded settings instance so a toggle
                            // in Settings and the widget's own hide button stay in
                            // sync without re-reading settings.json.
                            try { if (win.Settings.ShowDesktopWidget) WidgetWindow.OpenOrShow(win.Settings); }
                            catch (Exception ex) { Logger.Log("Desktop widget startup failed: " + ex.Message); }
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("Critical startup error: " + ex);
                            MessageBox.Show(I18n.T("app_name") + ":\n" + ex.Message, I18n.T("generic_error_title"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, Dialogs.RtlOptions());
                            splash.Close();
                            app.Shutdown();
                        }
                    };

                    // Safety timeout: guarantees the splash is force-closed no later than
                    // SafetyTimeoutMs after it appeared, regardless of what startup is
                    // doing, so it can never be stuck on screen indefinitely.
                    var safetyTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(SafetyTimeoutMs)
                    };
                    safetyTimer.Tick += (s, e) =>
                    {
                        safetyTimer.Stop();
                        Logger.Log("Splash safety timeout reached (" + SafetyTimeoutMs + "ms) - forcing handoff.");
                        MainWindow fallbackWin = null;
                        Exception fallbackError = null;
                        try { fallbackWin = new MainWindow(); }
                        catch (Exception ex) { fallbackError = ex; }
                        finishStartup(fallbackWin, fallbackError);
                    };
                    safetyTimer.Start();

                    MainWindow mainWindow = null;
                    Exception startupError = null;
                    try
                    {
                        // Created hidden - a WPF Window is never visible until Show() is
                        // called, so this cannot flash on screen before the splash closes.
                        mainWindow = new MainWindow();
                    }
                    catch (Exception ex)
                    {
                        startupError = ex;
                    }

                    int elapsedMs = (int)splashStopwatch.ElapsedMilliseconds;
                    int remainingMs = Math.Max(0, MinSplashDisplayMs - elapsedMs);

                    System.Threading.Tasks.Task.Delay(remainingMs).ContinueWith(_ =>
                    {
                        splash.Dispatcher.Invoke(() =>
                        {
                            safetyTimer.Stop();
                            finishStartup(mainWindow, startupError);
                        });
                    });

                    app.Run();
                }
                catch (Exception ex)
                {
                    Logger.Log("Critical startup error: " + ex);
                    MessageBox.Show(I18n.T("app_name") + ":\n" + ex.Message, I18n.T("generic_error_title"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, Dialogs.RtlOptions());
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
                        MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No, Dialogs.RtlOptions());
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
