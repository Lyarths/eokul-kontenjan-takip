using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EOkulKontenjanTakip.Models;

namespace EOkulKontenjanTakip.Services;

public static class StorageService
{
    public static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "E-Okul Checker"
    );

    public static readonly string DataFile = Path.Combine(AppDataDir, "eokul_data.json");
    public static readonly string SettingsFile = Path.Combine(AppDataDir, "ayarlar.json");
    public static readonly string LogFile = Path.Combine(AppDataDir, "uygulama.log");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    static StorageService()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
        }
        catch { }
    }

    public static AppDataStore LoadData()
    {
        if (!File.Exists(DataFile))
            return new AppDataStore();

        try
        {
            string json = File.ReadAllText(DataFile, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return new AppDataStore();

            var data = JsonSerializer.Deserialize<AppDataStore>(json, JsonOpts);
            return data ?? new AppDataStore();
        }
        catch
        {
            try
            {
                string backup = $"{DataFile}.bozuk.{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Copy(DataFile, backup, true);
            }
            catch { }
            return new AppDataStore();
        }
    }

    public static void SaveData(AppDataStore store)
    {
        try
        {
            string tmp = $"{DataFile}.tmp";
            string json = JsonSerializer.Serialize(store, JsonOpts);
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            File.Move(tmp, DataFile, true);
        }
        catch { }
    }

    public static AppSettings LoadSettings()
    {
        if (!File.Exists(SettingsFile))
            return new AppSettings();

        try
        {
            string json = File.ReadAllText(SettingsFile, Encoding.UTF8);
            var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
            return s ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            string tmp = $"{SettingsFile}.tmp";
            string json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            File.Move(tmp, SettingsFile, true);
        }
        catch { }
    }

    public static GradeQuota ParseGrade(SchoolCheckResult? res, string grade)
    {
        if (res?.Kontenjan == null || !res.Kontenjan.TryGetValue(grade, out var el))
            return new GradeQuota { Kontenjan = "-", Puan = "" };

        try
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                string k = el.TryGetProperty("kontenjan", out var kp) ? (kp.GetString() ?? "-") : "-";
                string p = el.TryGetProperty("puan", out var pp) ? (pp.GetString() ?? "") : "";
                return new GradeQuota { Kontenjan = k.Trim(), Puan = p.Trim() };
            }
            if (el.ValueKind == JsonValueKind.String)
            {
                return new GradeQuota { Kontenjan = (el.GetString() ?? "-").Trim(), Puan = "" };
            }
        }
        catch { }
        return new GradeQuota { Kontenjan = "-", Puan = "" };
    }

    public static (string OkulTuru, string YabanciDil) ParseMeta(SchoolCheckResult? res)
    {
        if (res?.Kontenjan == null || !res.Kontenjan.TryGetValue("detay", out var el))
            return ("", "");

        try
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                string tur = el.TryGetProperty("okul_turu", out var tp) ? (tp.GetString() ?? "") : "";
                string dil = el.TryGetProperty("yabanci_dil", out var dp) ? (dp.GetString() ?? "") : "";
                return (tur.Trim(), dil.Trim());
            }
        }
        catch { }
        return ("", "");
    }

    public static string? GetErrorMessage(SchoolCheckResult? res)
    {
        if (res?.Kontenjan == null || !res.Kontenjan.TryGetValue("hata", out var el))
            return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }

    public static bool HasAvailableQuota(SchoolCheckResult? res, List<string> trackedGrades)
    {
        if (res == null || GetErrorMessage(res) != null)
            return false;

        foreach (var g in trackedGrades)
        {
            var q = ParseGrade(res, g);
            if (!string.IsNullOrWhiteSpace(q.Kontenjan) &&
                q.Kontenjan != "0" && q.Kontenjan != "?" && q.Kontenjan != "-")
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Log dosyasını (uygulama.log) SİLMEDEN okulları, sonuçları ve EULA onayını sıfırlar.
    /// </summary>
    public static void ResetDataAndSettingsKeepLogs()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [BİLGİ] Program kullanıcı tarafından sıfırlandı (Okullar ve EULA onayı sıfırlandı, işlem kayıtları korundu)." + Environment.NewLine;
            File.AppendAllText(LogFile, logLine, Encoding.UTF8);
        }
        catch { }

        SaveData(new AppDataStore());
        var resetSettings = new AppSettings
        {
            Tema = "acik",
            SorguMotoru = "http",
            KontrolAraligi = 3600,
            BildirimWindows = true,
            BildirimPopup = false,
            BildirimFlash = false,
            EulaOnaylandi = false
        };
        SaveSettings(resetSettings);
    }

    public static void OpenAppDataFolder()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{AppDataDir}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }
}
