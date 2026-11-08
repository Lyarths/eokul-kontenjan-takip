using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace EOkulKontenjanTakip.Services;

public static class StartupService
{
    public static readonly string StartupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
    public static readonly string ShortcutPath = Path.Combine(StartupFolder, "eOkulKontenjanTakip.lnk");

    /// <summary>
    /// Kullanıcının masaüstündeki/klasördeki .exe dosyasını taşıması veya silmesi durumunda
    /// başlangıç kısayolunun asla bozulmaması için %LOCALAPPDATA%\E-Okul Checker\bin altında tutulan sabit kopya.
    /// </summary>
    public static readonly string PermanentBinDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "E-Okul Checker",
        "bin"
    );

    public static readonly string PermanentExePath = Path.Combine(PermanentBinDir, "eOkulKontenjanTakip.exe");

    public static string CurrentExePath => Environment.ProcessPath ?? "";

    /// <summary>
    /// Çalışan .exe dosyasını sabit %LOCALAPPDATA%\E-Okul Checker\bin\eOkulKontenjanTakip.exe konumuna kopyalar.
    /// Böylece kullanıcı asıl .exe dosyasının yerini değiştirse bile başlangıç kısayolu her zaman çalışır.
    /// </summary>
    public static void SyncPermanentBinary()
    {
        try
        {
            string current = CurrentExePath;
            if (string.IsNullOrEmpty(current) || !File.Exists(current))
                return;

            Directory.CreateDirectory(PermanentBinDir);

            if (!string.Equals(Path.GetFullPath(current), Path.GetFullPath(PermanentExePath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(current, PermanentExePath, true);
            }
        }
        catch { }
    }

    public static string? GetShortcutTarget()
    {
        if (!File.Exists(ShortcutPath))
            return null;

        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return null;

            object? shell = Activator.CreateInstance(shellType);
            if (shell == null)
                return null;

            object? shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { ShortcutPath }
            );
            if (shortcut == null)
                return null;

            Type scType = shortcut.GetType();
            object? targetObj = scType.InvokeMember(
                "TargetPath",
                BindingFlags.GetProperty,
                null,
                shortcut,
                null
            );
            return targetObj?.ToString();
        }
        catch
        {
            return null;
        }
    }

    public static bool IsRegistered()
    {
        if (!File.Exists(ShortcutPath))
            return false;

        string? target = GetShortcutTarget();
        if (string.IsNullOrEmpty(target))
            return false;

        return File.Exists(target);
    }

    /// <summary>
    /// Uygulama her açıldığında sabit kopyayı günceller ve başlangıç kısayolu varsa
    /// sabit %LOCALAPPDATA%\E-Okul Checker\bin\eOkulKontenjanTakip.exe yoluna bağlar.
    /// </summary>
    public static void RepairLegacyShortcutIfPresent()
    {
        try
        {
            SyncPermanentBinary();
            if (File.Exists(ShortcutPath))
            {
                string? target = GetShortcutTarget();
                if (string.IsNullOrEmpty(target) ||
                    !string.Equals(Path.GetFullPath(target), Path.GetFullPath(PermanentExePath), StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(target))
                {
                    Register();
                }
            }
        }
        catch { }
    }

    public static void OpenStartupFolder()
    {
        try
        {
            if (File.Exists(ShortcutPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{ShortcutPath}\"")
                {
                    UseShellExecute = true
                });
            }
            else if (Directory.Exists(StartupFolder))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{StartupFolder}\"")
                {
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    public static bool Register()
    {
        try
        {
            SyncPermanentBinary();

            string targetExe = File.Exists(PermanentExePath) ? PermanentExePath : CurrentExePath;
            if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe))
                return false;

            Directory.CreateDirectory(StartupFolder);
            string workDir = Path.GetDirectoryName(targetExe) ?? "";

            if (File.Exists(ShortcutPath))
            {
                try { File.Delete(ShortcutPath); } catch { }
            }

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return false;

            object? shell = Activator.CreateInstance(shellType);
            if (shell == null)
                return false;

            object? shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { ShortcutPath }
            );
            if (shortcut == null)
                return false;

            Type scType = shortcut.GetType();
            void SetProp(string name, object val) =>
                scType.InvokeMember(name, BindingFlags.SetProperty, null, shortcut, new[] { val });

            SetProp("TargetPath", targetExe);
            SetProp("Arguments", "--minimized");
            SetProp("WorkingDirectory", workDir);
            SetProp("IconLocation", $"{targetExe},0");
            SetProp("Description", "e-Okul Kontenjan Takip");

            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

            return IsRegistered();
        }
        catch
        {
            return false;
        }
    }

    public static bool Unregister()
    {
        try
        {
            if (File.Exists(ShortcutPath))
                File.Delete(ShortcutPath);
            return !File.Exists(ShortcutPath);
        }
        catch
        {
            return false;
        }
    }
}
