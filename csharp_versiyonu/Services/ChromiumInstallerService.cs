using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EOkulKontenjanTakip.Services;

public static class ChromiumInstallerService
{
    public static readonly string LocalAppRootDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "E-Okul Checker"
    );

    public static readonly string ChromiumRootDir = Path.Combine(LocalAppRootDir, "chromium");

    private const string DefaultDownloadUrl =
        "https://cdn.playwright.dev/builds/cft/151.0.7922.34/win64/chrome-headless-shell-win64.zip";

    private const string DefaultSubFolder = "chromium_headless_shell-1234";

    static ChromiumInstallerService()
    {
        try
        {
            Directory.CreateDirectory(LocalAppRootDir);
        }
        catch { }
    }

    public static string? FindInstalledChromiumExe()
    {
        try
        {
            if (!Directory.Exists(ChromiumRootDir))
                return null;

            string[] exeNames = { "chrome-headless-shell.exe", "chrome.exe", "headless_shell.exe" };
            foreach (var exe in exeNames)
            {
                var found = Directory.EnumerateFiles(ChromiumRootDir, exe, SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(found) && File.Exists(found))
                    return found;
            }
        }
        catch { }
        return null;
    }

    public static bool IsInstalled() => FindInstalledChromiumExe() != null;

    public static void OpenChromiumFolder()
    {
        try
        {
            string target = Directory.Exists(ChromiumRootDir) ? ChromiumRootDir : LocalAppRootDir;
            Directory.CreateDirectory(target);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{target}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private static async Task<(string DownloadUrl, string TargetDir)> ResolveDownloadTargetAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("python", "-X utf8 -m playwright install chromium --dry-run")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] = ChromiumRootDir;
            psi.Environment["PYTHONIOENCODING"] = "utf-8";

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                // Önce Chrome Headless Shell bloğunu, yoksa Chrome for Testing bloğunu yakala
                var blocks = output.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                var chosenBlock = blocks.FirstOrDefault(b => b.Contains("chromium_headless_shell", StringComparison.OrdinalIgnoreCase))
                                  ?? blocks.FirstOrDefault(b => b.Contains("chromium-", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(chosenBlock))
                {
                    var locMatch = Regex.Match(chosenBlock, @"Install location:\s*(.+)", RegexOptions.IgnoreCase);
                    var urlMatch = Regex.Match(chosenBlock, @"Download url:\s*(https?://\S+)", RegexOptions.IgnoreCase);
                    if (locMatch.Success && urlMatch.Success)
                    {
                        return (urlMatch.Groups[1].Value.Trim(), locMatch.Groups[1].Value.Trim());
                    }
                }
            }
        }
        catch { }

        return (DefaultDownloadUrl, Path.Combine(ChromiumRootDir, DefaultSubFolder));
    }

    /// <summary>
    /// Chromium paketini doğrudan %LOCALAPPDATA%\E-Okul Checker\chromium klasörüne
    /// canlı MB / Toplam MB ve yüzde ilerlemesi bildirerek indirir ve kurar.
    /// </summary>
    public static async Task<(bool Success, string Message)> DownloadAndInstallAsync(
        Action<double, double, double, string>? onProgress = null)
    {
        try
        {
            Directory.CreateDirectory(ChromiumRootDir);
            onProgress?.Invoke(0, 0, 0, "Chromium indirme bağlantısı hazırlanıyor...");

            var (downloadUrl, installDir) = await ResolveDownloadTargetAsync();
            string tempZip = Path.Combine(ChromiumRootDir, "chromium_paket.zip");

            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }

            if (Directory.Exists(installDir))
            {
                try { Directory.Delete(installDir, true); } catch { }
            }

            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.None
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(15)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? (105L * 1024 * 1024);
            double totalMb = totalBytes / (1024.0 * 1024.0);

            await using (var contentStream = await response.Content.ReadAsStreamAsync())
            await using (var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;
                var sw = Stopwatch.StartNew();

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (sw.ElapsedMilliseconds >= 75 || totalRead == totalBytes)
                    {
                        sw.Restart();
                        double downloadedMb = totalRead / (1024.0 * 1024.0);
                        if (downloadedMb > totalMb)
                            totalMb = downloadedMb;
                        double pct = totalMb > 0 ? Math.Min(98.0, (downloadedMb / totalMb) * 100.0) : 0;
                        onProgress?.Invoke(
                            pct,
                            downloadedMb,
                            totalMb,
                            $"Chromium indiriliyor: {downloadedMb:F1} MB / {totalMb:F1} MB (%{pct:F0})"
                        );
                    }
                }
            }

            onProgress?.Invoke(99, totalMb, totalMb, "İndirme tamamlandı, dosyalar çıkartılıyor (%LOCALAPPDATA%\\E-Okul Checker\\chromium)...");

            await Task.Run(() =>
            {
                Directory.CreateDirectory(installDir);
                ZipFile.ExtractToDirectory(tempZip, installDir, true);
                File.WriteAllText(Path.Combine(installDir, "INSTALLATION_COMPLETE"), "");
                try { File.Delete(tempZip); } catch { }
            });

            if (IsInstalled())
            {
                onProgress?.Invoke(100, totalMb, totalMb, $"Chromium kuruldu ({totalMb:F1} MB) — %LOCALAPPDATA%\\E-Okul Checker\\chromium");
                return (true, "Chromium tarayıcı başarıyla indirildi ve kuruldu.");
            }

            return (false, "Chromium paketi çıkartıldı ancak çalıştırılabilir dosya doğrulanamadı.");
        }
        catch (Exception ex)
        {
            return (false, $"Chromium indirilirken hata oluştu: {ex.Message}");
        }
    }
}
