using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EOkulKontenjanTakip.Models;

namespace EOkulKontenjanTakip.Services;

public class EOkulHttpSession : IDisposable
{
    private static readonly string[] EOkulUrls =
    {
        "https://e-okul.meb.gov.tr/OrtaOgretim/OKL/OOK06006.aspx",
        "https://e-okul.meb.gov.tr/OrtaOgretim/OKL/OOK06011.aspx"
    };

    private static readonly string[] ClosedKeywords =
    {
        "KAPATILMIŞTIR", "KAPATILMISTIR", "NAKİL ONAY", "NAKIL ONAY",
        "NAKİL KABUL", "NAKIL KABUL", "NAKİL BAŞVURU", "NAKIL BASVURU",
        "SİSTEMİ DUYURU", "SISTEMI DUYURU"
    };

    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _client;
    public string ActiveUrl { get; private set; } = EOkulUrls[0];

    public EOkulHttpSession()
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (eokul-kontenjan-csharp)");
        _client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tr-TR,tr;q=0.9,en-US;q=0.8");
    }

    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";
        string text = s.Replace('\u00A0', ' ')
            .Replace('İ', 'i').Replace('I', 'i').Replace('ı', 'i')
            .Replace('Ş', 's').Replace('ş', 's')
            .Replace('Ğ', 'g').Replace('ğ', 'g')
            .Replace('Ü', 'u').Replace('ü', 'u')
            .Replace('Ö', 'o').Replace('ö', 'o')
            .Replace('Ç', 'c').Replace('ç', 'c');
        return Regex.Replace(text, @"\s+", " ").Trim().ToLowerInvariant();
    }

    public static string? GetAttr(string tag, string attrName)
    {
        var m = Regex.Match(tag, $@"\b{Regex.Escape(attrName)}\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
        return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value) : null;
    }

    public static List<(string Value, string Text, bool Selected)> ParseOptions(string pageHtml, string selectName)
    {
        var result = new List<(string, string, bool)>();
        var m = Regex.Match(
            pageHtml,
            $@"<select\b[^>]*(?:name|id)=""{Regex.Escape(selectName)}""[^>]*>(.*?)</select>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!m.Success)
            return result;

        var optMatches = Regex.Matches(
            m.Groups[1].Value,
            @"(<option\b[^>]*>)(.*?)</option>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        foreach (Match om in optMatches)
        {
            string tag = om.Groups[1].Value;
            string val = (GetAttr(tag, "value") ?? "").Trim();
            string rawText = Regex.Replace(om.Groups[2].Value, @"\s+", " ");
            string text = WebUtility.HtmlDecode(rawText).Replace('\u00A0', ' ').Trim();
            bool selected = tag.Contains("selected", StringComparison.OrdinalIgnoreCase);
            result.Add((val, text, selected));
        }
        return result;
    }

    public static List<SelectOption> GetValidOptions(string pageHtml, string selectName)
    {
        var list = new List<SelectOption>();
        foreach (var (val, text, _) in ParseOptions(pageHtml, selectName))
        {
            if (!string.IsNullOrEmpty(val) && val != "-1" && !string.IsNullOrEmpty(text) && !text.StartsWith("Seçiniz", StringComparison.OrdinalIgnoreCase))
            {
                var (shortTitle, sub) = SelectOption.FormatGmTitle(text);
                list.Add(new SelectOption
                {
                    Value = val,
                    Text = text,
                    ShortTitle = shortTitle,
                    Subtitle = sub
                });
            }
        }
        return list;
    }

    public static string GetSelectedValue(string pageHtml, string selectName)
    {
        var opts = ParseOptions(pageHtml, selectName);
        if (opts.Count == 0)
            return "";
        foreach (var (v, _, sel) in opts)
        {
            if (sel)
                return v;
        }
        return opts[0].Value;
    }

    public static Dictionary<string, string> ExtractFormFields(string pageHtml)
    {
        var fields = new Dictionary<string, string>();
        var inputMatches = Regex.Matches(pageHtml, @"<input\b[^>]*>", RegexOptions.IgnoreCase);
        foreach (Match im in inputMatches)
        {
            string tag = im.Value;
            string? name = GetAttr(tag, "name");
            string type = (GetAttr(tag, "type") ?? "text").ToLowerInvariant();
            if (string.IsNullOrEmpty(name) || type is "submit" or "button" or "image" or "reset" or "file")
                continue;
            if ((type is "checkbox" or "radio") && !tag.Contains("checked", StringComparison.OrdinalIgnoreCase))
                continue;
            fields[name] = GetAttr(tag, "value") ?? "";
        }

        var selectMatches = Regex.Matches(pageHtml, @"<select\b[^>]*name=""([^""]+)""", RegexOptions.IgnoreCase);
        foreach (Match sm in selectMatches)
        {
            string name = sm.Groups[1].Value;
            fields[name] = GetSelectedValue(pageHtml, name);
        }
        return fields;
    }

    public static (string? Value, string? Text) FindOption(string pageHtml, string selectName, string query)
    {
        var candidates = ParseOptions(pageHtml, selectName)
            .Where(o => !string.IsNullOrEmpty(o.Value) && o.Value != "-1")
            .ToList();

        string target = Normalize(query);
        if (string.IsNullOrEmpty(target))
            return (null, null);

        foreach (var (v, t, _) in candidates)
        {
            if (Normalize(t) == target || v == query.Trim())
                return (v, t);
        }

        var codeMatch = Regex.Match(query, @"\((\d{5,8})\)");
        if (codeMatch.Success)
        {
            string code = codeMatch.Groups[1].Value;
            foreach (var (v, t, _) in candidates)
            {
                if (v == code || t.Contains($"({code})"))
                    return (v, t);
            }
        }

        foreach (var (v, t, _) in candidates)
        {
            string nt = Normalize(t);
            if (nt.Contains(target) || (nt.Length > 3 && target.Contains(nt)))
                return (v, t);
        }

        return (null, null);
    }

    public static bool IsErrorPage(string pageHtml) =>
        pageHtml.Contains("images/hata.jpg", StringComparison.OrdinalIgnoreCase);

    public static string? CheckSiteClosedMessage(string pageHtml)
    {
        if (Regex.IsMatch(pageHtml, @"name=""ddlTercihTuru""|id=""ddlTercihTuru""", RegexOptions.IgnoreCase))
            return null;

        string stripped = Regex.Replace(pageHtml, @"<script\b[^>]*>.*?</script>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        stripped = Regex.Replace(stripped, @"<style\b[^>]*>.*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        stripped = Regex.Replace(stripped, @"<[^>]+>", " ");
        stripped = WebUtility.HtmlDecode(stripped).Replace('\u00A0', ' ');
        stripped = Regex.Replace(stripped, @"\s+", " ").Trim();

        string up = stripped.ToUpperInvariant();
        foreach (var kw in ClosedKeywords)
        {
            if (up.Contains(kw))
                return stripped.Length > 380 ? stripped[..380] : stripped;
        }
        return null;
    }

    public async Task<string> OpenInitialPageAsync()
    {
        Exception? lastErr = null;
        string lastPage = "";
        foreach (var url in EOkulUrls)
        {
            try
            {
                ActiveUrl = url;
                using var resp = await _client.GetAsync(url);
                byte[] bytes = await resp.Content.ReadAsByteArrayAsync();
                string html = Encoding.UTF8.GetString(bytes);
                lastPage = html;
                if (IsErrorPage(html))
                    continue;
                if (!Regex.IsMatch(html, @"<input\b[^>]*(?:id|name)=""[^""]*(?:Kimlik|TCKN|TCNo)[^""]*""", RegexOptions.IgnoreCase))
                    return html;
            }
            catch (Exception ex)
            {
                lastErr = ex;
            }
        }
        if (!string.IsNullOrEmpty(lastPage))
            return lastPage;
        throw lastErr ?? new Exception("e-Okul sayfasına ulaşılamadı.");
    }

    public async Task<string> PostFormAsync(Dictionary<string, string> form, int delayMs = 800)
    {
        if (delayMs > 0)
            await Task.Delay(delayMs);

        using var content = new FormUrlEncodedContent(form);
        using var resp = await _client.PostAsync(ActiveUrl, content);
        byte[] bytes = await resp.Content.ReadAsByteArrayAsync();
        return Encoding.UTF8.GetString(bytes);
    }

    public async Task<string> PostBackAsync(string currentHtml, string selectName, string value, int delayMs = 750)
    {
        var form = ExtractFormFields(currentHtml);
        form[selectName] = value;
        form["__EVENTTARGET"] = selectName;
        form["__EVENTARGUMENT"] = "";
        return await PostFormAsync(form, delayMs);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

public static class EOkulQuotaService
{
    public static async Task<SchoolCheckResult> CheckSchoolQuotaAsync(TrackedSchool school, string engine = "http")
    {
        if (string.Equals(engine, "chromium", StringComparison.OrdinalIgnoreCase))
        {
            var pyRes = await TryCheckViaPythonChromiumAsync(school);
            if (pyRes != null)
                return pyRes;
        }

        try
        {
            using var session = new EOkulHttpSession();
            string page = await session.OpenInitialPageAsync();
            if (EOkulHttpSession.IsErrorPage(page))
                return MakeError("e-Okul sunucusu ilk açılışta hata sayfası döndürdü.");

            string? closedMsg = EOkulHttpSession.CheckSiteClosedMessage(page);
            if (closedMsg != null)
            {
                return new SchoolCheckResult
                {
                    SonKontrol = DateTime.Now.ToString("HH:mm:ss"),
                    SiteKapali = true,
                    SiteKapaliMesaji = closedMsg,
                    Kontenjan = MakeErrorDict(closedMsg)
                };
            }

            var steps = new (string Label, string SelectName, string Query)[]
            {
                ("Genel Müdürlük", "ddlTercihTuru", school.Gm),
                ("İl", "ddlIl", school.Il),
                ("İlçe", "ddlIlce", school.Ilce)
            };

            foreach (var (label, selectName, query) in steps)
            {
                var (val, _) = EOkulHttpSession.FindOption(page, selectName, query);
                if (val == null)
                    return MakeError($"{label} bulunamadı: '{query}'");

                page = await session.PostBackAsync(page, selectName, val, 850);
                if (EOkulHttpSession.IsErrorPage(page))
                    return MakeError($"Sunucu '{label}' adımında hata sayfası döndürdü.");
            }

            var (schoolVal, _) = EOkulHttpSession.FindOption(page, "ddlOkul", school.Okul);
            if (schoolVal == null)
                return MakeError($"Okul listede bulunamadı: '{school.Okul}'");

            string? btnTag = FindBtnGirisTag(page);
            if (btnTag == null)
                return MakeError("'btnGiris' sorgulama düğmesi sayfada bulunamadı.");

            var form = EOkulHttpSession.ExtractFormFields(page);
            form["ddlOkul"] = schoolVal;
            form["hdnKontrol"] = "evet";
            string btnType = (EOkulHttpSession.GetAttr(btnTag, "type") ?? "submit").ToLowerInvariant();
            if (btnType == "image")
            {
                form["btnGiris.x"] = "5";
                form["btnGiris.y"] = "5";
            }
            else
            {
                form["btnGiris"] = EOkulHttpSession.GetAttr(btnTag, "value") ?? "";
            }

            page = await session.PostFormAsync(form, 850);
            if (EOkulHttpSession.IsErrorPage(page))
                return MakeError("Sunucu 'Giriş' adımında hata sayfası döndürdü.");

            var table = ParseDgListeTable(page);
            if (table == null || table.Count < 2)
                return MakeError("Kontenjan tablosu (dgListe) sayfada bulunamadı.");

            return BuildResultFromTable(table);
        }
        catch (Exception ex)
        {
            return MakeError($"Bağlantı / Sorgu hatası: {ex.Message}");
        }
    }

    private static async Task<SchoolCheckResult?> TryCheckViaPythonChromiumAsync(TrackedSchool school)
    {
        string? exePath = ChromiumInstallerService.FindInstalledChromiumExe();
        if (string.IsNullOrEmpty(exePath))
        {
            return MakeError("Chromium tarayıcı kurulu değil. Ayarlar > Chromium Tarayıcı bölümünden indirebilir veya Düz İstek (HTTP) modunu kullanabilirsiniz.");
        }

        try
        {
            string runnerPath = Path.Combine(ChromiumInstallerService.LocalAppRootDir, "chromium_runner.py");
            string scriptCode = @"# -*- coding: utf-8 -*-
import sys, json, re
from playwright.sync_api import sync_playwright

TR_MAP = str.maketrans('İIıiŞşĞğÜüÖöÇç', 'iiisssgguuoocc')
def norm(s):
    return re.sub(r'\s+', ' ', (s or '').translate(TR_MAP).lower()).strip()

def select_opt(pg, sel_id, target):
    opts = pg.eval_on_selector_all(f'#{sel_id} option', 'els => els.map(e => ({value: e.value, text: e.textContent}))')
    t = norm(target)
    m = next((o for o in opts if o['value'] and o['value'] != '-1' and (t == norm(o['text']) or t in norm(o['text']))), None)
    if not m:
        return False
    try:
        with pg.expect_navigation(wait_until='networkidle', timeout=12000):
            pg.evaluate('({id,val})=>{const e=document.getElementById(id);e.value=val;e.dispatchEvent(new Event(""change"",{bubbles:true}))}', {'id': sel_id, 'val': m['value']})
    except Exception:
        pg.wait_for_timeout(1500)
    return True

exe_path = sys.argv[1]
d = json.loads(sys.argv[2])
with sync_playwright() as p:
    br = p.chromium.launch(headless=True, executable_path=exe_path)
    pg = br.new_page()
    pg.goto('https://e-okul.meb.gov.tr/OrtaOgretim/OKL/IOK07006.aspx', wait_until='networkidle', timeout=25000)
    select_opt(pg, 'ddlTercihTuru', d['gm'])
    select_opt(pg, 'ddlIl', d['il'])
    select_opt(pg, 'ddlIlce', d['ilce'])
    select_opt(pg, 'ddlOkul', d['okul'])
    try:
        with pg.expect_navigation(wait_until='networkidle', timeout=10000):
            pg.click('#btnGiris')
    except Exception:
        pg.wait_for_timeout(1500)
    pg.wait_for_selector('#dgListe', timeout=8000)
    html = pg.content()
    br.close()
    sys.stdout.write(html)
";
            File.WriteAllText(runnerPath, scriptCode, new UTF8Encoding(false));

            string argJson = JsonSerializer.Serialize(school);
            var psi = new ProcessStartInfo("python", $"-X utf8 \"{runnerPath}\" \"{exePath}\" \"{argJson.Replace("\"", "\\\"")}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] = ChromiumInstallerService.ChromiumRootDir;
            psi.Environment["PYTHONIOENCODING"] = "utf-8";

            using var proc = Process.Start(psi);
            if (proc == null)
                return null;
            string htmlOut = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(htmlOut))
            {
                var table = ParseDgListeTable(htmlOut);
                if (table != null && table.Count >= 2)
                {
                    return BuildResultFromTable(table);
                }
            }
        }
        catch { }
        return null;
    }

    private static string? FindBtnGirisTag(string pageHtml)
    {
        var matches = Regex.Matches(pageHtml, @"<(?:input|button)\b[^>]*>", RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            string tag = m.Value;
            if (EOkulHttpSession.GetAttr(tag, "id") == "btnGiris" ||
                EOkulHttpSession.GetAttr(tag, "name") == "btnGiris")
            {
                return tag;
            }
        }
        return null;
    }

    private static List<List<string>>? ParseDgListeTable(string pageHtml)
    {
        var m = Regex.Match(
            pageHtml,
            @"<table\b[^>]*id=""dgListe""[^>]*>(.*?)</table>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );
        if (!m.Success)
            return null;

        var rows = new List<List<string>>();
        var trMatches = Regex.Matches(m.Groups[1].Value, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match tr in trMatches)
        {
            var cellMatches = Regex.Matches(tr.Groups[1].Value, @"<t[dh]\b[^>]*>(.*?)</t[dh]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var cells = new List<string>();
            foreach (Match c in cellMatches)
            {
                string text = Regex.Replace(c.Groups[1].Value, @"<[^>]+>", " ");
                text = WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');
                text = Regex.Replace(text, @"\s+", " ").Trim();
                cells.Add(text);
            }
            if (cells.Count > 0)
                rows.Add(cells);
        }
        return rows.Count > 0 ? rows : null;
    }

    private static SchoolCheckResult BuildResultFromTable(List<List<string>> table)
    {
        var headers = table[0];
        var dataRows = table.Skip(1).ToList();
        var firstRow = dataRows[0];
        var normHeaders = headers.Select(EOkulHttpSession.Normalize).ToList();

        var rawDict = new Dictionary<string, object>();
        foreach (var g in new[] { "9", "10", "11", "12" })
        {
            int? kIdx = null;
            int? pIdx = null;
            for (int i = 0; i < normHeaders.Count; i++)
            {
                string nh = normHeaders[i];
                if (nh.Contains($"{g}.sinif") || nh.Contains($"{g}. sinif"))
                {
                    if (nh.Contains("puan"))
                        pIdx = i;
                    else if (nh.Contains("kontenjan") || kIdx == null)
                        kIdx = i;
                }
            }

            if (kIdx.HasValue && kIdx.Value < firstRow.Count)
            {
                string kVal;
                if (dataRows.Count == 1)
                {
                    kVal = firstRow[kIdx.Value].Trim();
                }
                else
                {
                    int sum = 0;
                    bool allNumeric = true;
                    foreach (var r in dataRows)
                    {
                        string cell = kIdx.Value < r.Count ? r[kIdx.Value].Trim() : "0";
                        if (int.TryParse(cell, out int n))
                            sum += n;
                        else
                            allNumeric = false;
                    }
                    kVal = allNumeric ? sum.ToString() : firstRow[kIdx.Value].Trim();
                }

                string pVal = (pIdx.HasValue && pIdx.Value < firstRow.Count) ? firstRow[pIdx.Value].Trim() : "";
                rawDict[g] = new GradeQuota { Kontenjan = kVal, Puan = pVal };
            }
            else
            {
                rawDict[g] = new GradeQuota { Kontenjan = "?", Puan = "" };
            }
        }

        int? turIdx = normHeaders.Select((nh, idx) => (nh, idx)).FirstOrDefault(x => x.nh.Contains("okul turu") || x.nh.Contains("alan")).idx;
        if (turIdx == 0 && !normHeaders[0].Contains("okul turu") && !normHeaders[0].Contains("alan"))
            turIdx = null;

        int? dilIdx = normHeaders.Select((nh, idx) => (nh, idx)).FirstOrDefault(x => x.nh.Contains("yabanci dil") || x.nh.Contains("dil")).idx;
        if (dilIdx == 0 && !normHeaders[0].Contains("dil"))
            dilIdx = null;

        string okulTuru = (turIdx.HasValue && turIdx.Value < firstRow.Count) ? firstRow[turIdx.Value].Trim() : "";
        var diller = new List<string>();
        if (dilIdx.HasValue)
        {
            foreach (var r in dataRows)
            {
                if (dilIdx.Value < r.Count && !string.IsNullOrWhiteSpace(r[dilIdx.Value]))
                {
                    string d = r[dilIdx.Value].Trim();
                    if (!diller.Contains(d))
                        diller.Add(d);
                }
            }
        }

        rawDict["detay"] = new TableDetail
        {
            OkulTuru = okulTuru,
            YabanciDil = string.Join(", ", diller),
            Basliklar = headers,
            Satirlar = dataRows
        };

        string serialized = JsonSerializer.Serialize(rawDict);
        var jsonDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(serialized) ?? new();

        return new SchoolCheckResult
        {
            Kontenjan = jsonDict,
            SonKontrol = DateTime.Now.ToString("HH:mm:ss"),
            SiteKapali = false
        };
    }

    private static Dictionary<string, JsonElement> MakeErrorDict(string msg)
    {
        string json = JsonSerializer.Serialize(new Dictionary<string, string> { ["hata"] = msg });
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();
    }

    private static SchoolCheckResult MakeError(string msg) => new()
    {
        Kontenjan = MakeErrorDict(msg),
        SonKontrol = DateTime.Now.ToString("HH:mm:ss"),
        SiteKapali = false
    };
}

public class CascadingDropdownSession
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private EOkulHttpSession? _session;
    private string? _pageGm;
    private string? _selectedGm;
    private string? _pageIl;
    private string? _selectedIl;
    private string? _pageIlce;

    public static List<SelectOption> GetDefaultGmOptions()
    {
        string[] rawList =
        {
            "Ortaöğretim Genel Müdürlüğü Fen ve Sosyal Bilimler Liseleri Okul Listesi",
            "Ortaöğretim Genel Müdürlüğü Anadolu Liseleri Okul Listesi",
            "Ortaöğretim Genel Müdürlüğü Güzel Sanatlar ve Spor Liseleri Okul Listesi",
            "Din Öğretimi Genel Müdürlüğü Anadolu İmam Hatip Lisesi 1",
            "Mesleki ve Teknik Eğitim Genel Müdürlüğü"
        };

        var list = new List<SelectOption>();
        for (int i = 0; i < rawList.Length; i++)
        {
            var (st, sub) = SelectOption.FormatGmTitle(rawList[i]);
            list.Add(new SelectOption
            {
                Value = (i + 1).ToString(),
                Text = rawList[i],
                ShortTitle = st,
                Subtitle = sub
            });
        }
        return list;
    }

    public async Task<(List<SelectOption> Options, string? Error, bool SiteClosed)> LoadGmAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _session?.Dispose();
            _session = new EOkulHttpSession();
            string s = await _session.OpenInitialPageAsync();
            if (EOkulHttpSession.IsErrorPage(s))
                return (new(), "e-Okul hata sayfası döndürdü.", false);

            string? closed = EOkulHttpSession.CheckSiteClosedMessage(s);
            if (closed != null)
                return (new(), closed, true);

            _pageGm = s;
            _selectedGm = null;
            _pageIl = null;
            _selectedIl = null;
            _pageIlce = null;
            return (EOkulHttpSession.GetValidOptions(s, "ddlTercihTuru"), null, false);
        }
        catch (Exception ex)
        {
            return (new(), ex.Message, false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(List<SelectOption> Options, string? Error)> LoadIlAsync(string gmText)
    {
        await _lock.WaitAsync();
        try
        {
            if (_session == null || string.IsNullOrEmpty(_pageGm))
            {
                _session = new EOkulHttpSession();
                _pageGm = await _session.OpenInitialPageAsync();
            }

            var (val, _) = EOkulHttpSession.FindOption(_pageGm, "ddlTercihTuru", gmText);
            if (val == null)
            {
                _session.Dispose();
                _session = new EOkulHttpSession();
                _pageGm = await _session.OpenInitialPageAsync();
                (val, _) = EOkulHttpSession.FindOption(_pageGm, "ddlTercihTuru", gmText);
                if (val == null)
                    return (new(), $"Genel Müdürlük bulunamadı: {gmText}");
            }

            string s = await _session.PostBackAsync(_pageGm, "ddlTercihTuru", val, 600);
            if (EOkulHttpSession.IsErrorPage(s))
            {
                _session.Dispose();
                _session = new EOkulHttpSession();
                _pageGm = await _session.OpenInitialPageAsync();
                (val, _) = EOkulHttpSession.FindOption(_pageGm, "ddlTercihTuru", gmText);
                s = await _session.PostBackAsync(_pageGm, "ddlTercihTuru", val!, 600);
                if (EOkulHttpSession.IsErrorPage(s))
                    return (new(), "e-Okul sunucusu hata sayfası döndürdü.");
            }

            _selectedGm = gmText;
            _pageIl = s;
            _selectedIl = null;
            _pageIlce = null;
            return (EOkulHttpSession.GetValidOptions(s, "ddlIl"), null);
        }
        catch (Exception ex)
        {
            return (new(), ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(List<SelectOption> Options, string? Error)> LoadIlceAsync(string gmText, string ilText)
    {
        if (_session == null || string.IsNullOrEmpty(_pageIl) || _selectedGm != gmText)
        {
            var (_, err) = await LoadIlAsync(gmText);
            if (err != null)
                return (new(), err);
        }

        await _lock.WaitAsync();
        try
        {
            var (val, _) = EOkulHttpSession.FindOption(_pageIl!, "ddlIl", ilText);
            if (val == null)
                return (new(), $"İl bulunamadı: {ilText}");

            string s = await _session!.PostBackAsync(_pageIl!, "ddlIl", val, 600);
            if (EOkulHttpSession.IsErrorPage(s))
                return (new(), "İlçeler yüklenirken sunucu hata sayfası döndürdü.");

            _selectedIl = ilText;
            _pageIlce = s;
            return (EOkulHttpSession.GetValidOptions(s, "ddlIlce"), null);
        }
        catch (Exception ex)
        {
            return (new(), ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(List<SelectOption> Options, string? Error)> LoadOkulAsync(string gmText, string ilText, string ilceText)
    {
        if (_session == null || string.IsNullOrEmpty(_pageIlce) || _selectedGm != gmText || _selectedIl != ilText)
        {
            var (_, err) = await LoadIlceAsync(gmText, ilText);
            if (err != null)
                return (new(), err);
        }

        await _lock.WaitAsync();
        try
        {
            var (val, _) = EOkulHttpSession.FindOption(_pageIlce!, "ddlIlce", ilceText);
            if (val == null)
                return (new(), $"İlçe bulunamadı: {ilceText}");

            string s = await _session!.PostBackAsync(_pageIlce!, "ddlIlce", val, 600);
            if (EOkulHttpSession.IsErrorPage(s))
                return (new(), "Okullar yüklenirken sunucu hata sayfası döndürdü.");

            return (EOkulHttpSession.GetValidOptions(s, "ddlOkul"), null);
        }
        catch (Exception ex)
        {
            return (new(), ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }
}
