using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using EOkulKontenjanTakip.Services;

namespace EOkulKontenjanTakip.Models;

public class TrackedSchool
{
    [JsonPropertyName("gm")]
    public string Gm { get; set; } = "";

    [JsonPropertyName("il")]
    public string Il { get; set; } = "";

    [JsonPropertyName("ilce")]
    public string Ilce { get; set; } = "";

    [JsonPropertyName("okul")]
    public string Okul { get; set; } = "";

    [JsonPropertyName("siniflar")]
    public List<string> Siniflar { get; set; } = new() { "9", "10", "11", "12" };

    [JsonIgnore]
    public string Key => $"{Okul} / {Ilce}";
}

public class GradeQuota
{
    [JsonPropertyName("kontenjan")]
    public string Kontenjan { get; set; } = "0";

    [JsonPropertyName("puan")]
    public string Puan { get; set; } = "";
}

public class TableDetail
{
    [JsonPropertyName("okul_turu")]
    public string OkulTuru { get; set; } = "";

    [JsonPropertyName("yabanci_dil")]
    public string YabanciDil { get; set; } = "";

    [JsonPropertyName("basliklar")]
    public List<string> Basliklar { get; set; } = new();

    [JsonPropertyName("satirlar")]
    public List<List<string>> Satirlar { get; set; } = new();
}

public class SchoolCheckResult
{
    [JsonPropertyName("kontenjan")]
    public Dictionary<string, JsonElement> Kontenjan { get; set; } = new();

    [JsonPropertyName("son_kontrol")]
    public string SonKontrol { get; set; } = "-";

    [JsonPropertyName("site_kapali")]
    public bool SiteKapali { get; set; }

    [JsonPropertyName("site_kapali_mesaji")]
    public string SiteKapaliMesaji { get; set; } = "";
}

public class AppDataStore
{
    [JsonPropertyName("okullar")]
    public List<TrackedSchool> Okullar { get; set; } = new();

    [JsonPropertyName("sonuclar")]
    public Dictionary<string, SchoolCheckResult> Sonuclar { get; set; } = new();
}

public class AppSettings
{
    [JsonPropertyName("tema")]
    public string Tema { get; set; } = "acik";

    [JsonPropertyName("sorgu_motoru")]
    public string SorguMotoru { get; set; } = "http";

    [JsonPropertyName("kontrol_araligi")]
    public int KontrolAraligi { get; set; } = 3600;

    [JsonPropertyName("bildirim_windows")]
    public bool BildirimWindows { get; set; } = true;

    [JsonPropertyName("bildirim_popup")]
    public bool BildirimPopup { get; set; } = false;

    [JsonPropertyName("bildirim_flash")]
    public bool BildirimFlash { get; set; } = false;

    [JsonPropertyName("eula_onaylandi")]
    public bool EulaOnaylandi { get; set; } = false;
}

public class SelectOption
{
    public string Value { get; set; } = "";
    public string Text { get; set; } = "";
    public string ShortTitle { get; set; } = "";
    public string Subtitle { get; set; } = "";

    public static (string ShortTitle, string Subtitle) FormatGmTitle(string rawText)
    {
        string norm = EOkulHttpSession.Normalize(rawText);
        if (norm.Contains("imam hatip") || norm.Contains("din ogretimi"))
            return ("Anadolu İmam Hatip Liseleri", "Din Öğretimi Genel Müdürlüğü");
        if (norm.Contains("fen ve sosyal"))
            return ("Fen ve Sosyal Bilimler Liseleri", "Ortaöğretim Genel Müdürlüğü");
        if (norm.Contains("guzel sanatlar") || norm.Contains("spor lise"))
            return ("Güzel Sanatlar ve Spor Liseleri", "Ortaöğretim Genel Müdürlüğü");
        if (norm.Contains("mesleki") || norm.Contains("teknik"))
            return ("Mesleki ve Teknik Anadolu Liseleri", "Mesleki ve Teknik Eğitim GM");
        if (norm.Contains("ozel egitim") || norm.Contains("rehberlik"))
            return ("Özel Eğitim ve Rehberlik Okulları", "Özel Eğitim ve Rehberlik GM");
        if (norm.Contains("anadolu lise"))
            return ("Anadolu Liseleri", "Ortaöğretim Genel Müdürlüğü");

        return ((rawText ?? "").Replace("Okul Listesi", "").Trim(), "MEB e-Okul");
    }
}
