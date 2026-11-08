using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using EOkulKontenjanTakip.Services;

namespace EOkulKontenjanTakip;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\EOkulKontenjanTakip_SingleInstance_Mutex";
    private const string EventName = @"Local\EOkulKontenjanTakip_WakeUp_Event";

    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _wakeUpEvent;
    private static bool _mutexOwned;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool isRestarting = e.Args.Any(a => a.Equals("--restarting", StringComparison.OrdinalIgnoreCase));
        bool startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

        // Her çalıştırmada sabit kopyayı (%LOCALAPPDATA%\E-Okul Checker\bin\eOkulKontenjanTakip.exe) ve kısayolu güncel tut
        StartupService.RepairLegacyShortcutIfPresent();

        // Tek Örnek (Single-Instance) Kontrolü:
        // Uygulama zaten açıksa (veya gizli simgelerdeyse) ikinci bir pencere açmak yerine mevcut olanı öne getir.
        try
        {
            _wakeUpEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            _singleInstanceMutex = new Mutex(false, MutexName);

            try
            {
                _mutexOwned = _singleInstanceMutex.WaitOne(isRestarting ? 2500 : 0, false);
            }
            catch (AbandonedMutexException)
            {
                _mutexOwned = true;
            }

            if (!_mutexOwned)
            {
                // Açık olan (veya sistem tepsisinde gizlenen) örneğe "Pencereyi Öne Getir" sinyali gönder ve çık
                _wakeUpEvent.Set();
                Environment.Exit(0);
                return;
            }

            // Arka planda ikinci bir .exe tıklamasını dinle
            Task.Run(() =>
            {
                while (_wakeUpEvent != null)
                {
                    try
                    {
                        if (_wakeUpEvent.WaitOne())
                        {
                            Current?.Dispatcher.BeginInvoke(() =>
                            {
                                if (Current.MainWindow is MainWindow mw)
                                {
                                    mw.BringWindowToFront();
                                }
                            });
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            });
        }
        catch { }

        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception.ToString());
            args.Handled = true;
            if (MainWindow == null || !MainWindow.IsLoaded)
            {
                MessageBox.Show($"Başlangıç hatası:\n{args.Exception.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                ReleaseSingleInstance();
                Environment.Exit(1);
            }
        };

        var settings = StorageService.LoadSettings();
        if (string.IsNullOrEmpty(settings.Tema))
            settings.Tema = "acik";
        ApplyTheme(settings.Tema);

        var win = new MainWindow(startMinimized);
        MainWindow = win;
        win.Show();
    }

    public static void ReleaseSingleInstance()
    {
        try
        {
            if (_mutexOwned && _singleInstanceMutex != null)
            {
                _singleInstanceMutex.ReleaseMutex();
                _mutexOwned = false;
            }
        }
        catch { }

        try
        {
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }
        catch { }

        try
        {
            var ev = _wakeUpEvent;
            _wakeUpEvent = null;
            ev?.Dispose();
        }
        catch { }
    }

    public static void ApplyWindowDwmAttributes(Window window)
    {
        try
        {
            if (Current?.MainWindow != null && window != Current.MainWindow && Current.MainWindow.Icon != null)
            {
                window.Icon = Current.MainWindow.Icon;
            }

            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var settings = StorageService.LoadSettings();
            int dark = string.Equals(settings.Tema, "koyu", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

            int cornerPref = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int));
        }
        catch { }
    }

    public static void ApplyTheme(string themeName)
    {
        var res = Current.Resources;
        bool isDark = string.Equals(themeName, "koyu", StringComparison.OrdinalIgnoreCase);

        Color C(string hex) => (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        void SetBrush(string key, string hex) => res[key] = new SolidColorBrush(C(hex));

        if (isDark)
        {
            SetBrush("BgBrush", "#0F141C");
            SetBrush("TopBarBrush", "#151C27");
            SetBrush("SurfaceBrush", "#18202C");
            SetBrush("SurfaceHoverBrush", "#202B3A");
            SetBrush("CardInnerBrush", "#121822");
            SetBrush("BorderBrush", "#263244");
            SetBrush("BorderHoverBrush", "#37475E");
            SetBrush("TextPrimaryBrush", "#F1F5F9");
            SetBrush("TextSecondaryBrush", "#94A3B8");
            SetBrush("TextMutedBrush", "#64748B");
            SetBrush("AccentBrush", "#3B82F6");
            SetBrush("AccentHoverBrush", "#2563EB");
            SetBrush("AccentSoftBrush", "#172554");
            SetBrush("AccentBorderBrush", "#2563EB");
            SetBrush("GreenBrush", "#10B981");
            SetBrush("GreenSoftBrush", "#063E2E");
            SetBrush("GreenBorderBrush", "#059669");
            SetBrush("DangerBrush", "#EF4444");
            SetBrush("DangerSoftBrush", "#3B1219");
            SetBrush("AmberBrush", "#F59E0B");
            SetBrush("AmberSoftBrush", "#3B2A10");
        }
        else
        {
            SetBrush("BgBrush", "#F4F6F9");
            SetBrush("TopBarBrush", "#FFFFFF");
            SetBrush("SurfaceBrush", "#FFFFFF");
            SetBrush("SurfaceHoverBrush", "#F8FAFC");
            SetBrush("CardInnerBrush", "#F8FAFC");
            SetBrush("BorderBrush", "#E2E8F0");
            SetBrush("BorderHoverBrush", "#CBD5E1");
            SetBrush("TextPrimaryBrush", "#0F172A");
            SetBrush("TextSecondaryBrush", "#475569");
            SetBrush("TextMutedBrush", "#94A3B8");
            SetBrush("AccentBrush", "#2563EB");
            SetBrush("AccentHoverBrush", "#1D4ED8");
            SetBrush("AccentSoftBrush", "#EFF6FF");
            SetBrush("AccentBorderBrush", "#93C5FD");
            SetBrush("GreenBrush", "#059669");
            SetBrush("GreenSoftBrush", "#ECFDF5");
            SetBrush("GreenBorderBrush", "#6EE7B7");
            SetBrush("DangerBrush", "#DC2626");
            SetBrush("DangerSoftBrush", "#FEF2F2");
            SetBrush("AmberBrush", "#D97706");
            SetBrush("AmberSoftBrush", "#FFFBEB");
        }
    }

    private static void LogCrash(string text)
    {
        try
        {
            string path = Path.Combine(StorageService.AppDataDir, "hata_gunlugu.txt");
            File.AppendAllText(path, $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{text}\n");
        }
        catch { }
    }
}
