using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using EOkulKontenjanTakip.Models;
using EOkulKontenjanTakip.Services;
using WinForms = System.Windows.Forms;

namespace EOkulKontenjanTakip;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private readonly bool _startMinimized;
    private AppDataStore _data;
    private AppSettings _settings;
    private readonly CascadingDropdownSession _dropdownSession = new();
    private readonly HashSet<string> _activeChecks = new();
    private bool _isRefreshingAll;
    private bool _suppressSettingEvents;
    private bool _trayHintShown;
    private int _countdownSeconds;

    private List<SelectOption> _gmOptions = new();
    private string _selectedGm = "";
    private List<string> _allIlOptions = new();
    private string _selectedIl = "";
    private List<string> _allIlceOptions = new();
    private string _selectedIlce = "";
    private List<string> _allOkulOptions = new();
    private string _selectedOkul = "";
    private bool _suppressListSelection;

    private WinForms.NotifyIcon? _trayIcon;
    private readonly DispatcherTimer _countdownTimer;

    public MainWindow(bool startMinimized = false)
    {
        _startMinimized = startMinimized;
        _data = StorageService.LoadData();
        _settings = StorageService.LoadSettings();
        if (string.IsNullOrEmpty(_settings.Tema))
            _settings.Tema = "acik";

        App.ApplyTheme(_settings.Tema);
        InitializeComponent();

        _countdownSeconds = _settings.KontrolAraligi > 0 ? _settings.KontrolAraligi : 3600;
        StartupPathText.Text = $"Konum: {StartupService.StartupFolder}";
        AppDataPathText.Text = $"Konum: {StorageService.AppDataDir}";

        HookSearchBoxKeyboardShortcuts();

        SourceInitialized += (_, _) => ApplyWin11WindowAttributes();
        Loaded += async (_, _) =>
        {
            StartupService.RepairLegacyShortcutIfPresent();
            LoadPersistedLogs();
            SyncSettingsToUi();
            RenderSchoolCards();
            SetupSystemTray();
            InitDefaultGmCardsOffline();

            if (!_settings.EulaOnaylandi)
            {
                var eulaWin = new EulaWindow(readOnlyMode: false) { Owner = this };
                eulaWin.ShowDialog();
                if (!eulaWin.Accepted)
                {
                    ExitApplication();
                    return;
                }
                _settings.EulaOnaylandi = true;
                StorageService.SaveSettings(_settings);
                AppendLog("Kullanım Bilgilendirmesi (EULA) onaylandı.", "basari");
            }
            else if (_startMinimized)
            {
                Hide();
            }

            AppendLog("Uygulama hazır.", "bilgi");
            await LoadGmListAsync();
        };

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += CountdownTimer_Tick;
        _countdownTimer.Start();
    }

    private void HookSearchBoxKeyboardShortcuts()
    {
        IlSearchBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { IlPopup.IsOpen = false; e.Handled = true; }
            else if (e.Key == Key.Enter && IlOptionsList.Items.Count > 0) { IlOptionsList.SelectedIndex = 0; e.Handled = true; }
        };
        IlceSearchBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { IlcePopup.IsOpen = false; e.Handled = true; }
            else if (e.Key == Key.Enter && IlceOptionsList.Items.Count > 0) { IlceOptionsList.SelectedIndex = 0; e.Handled = true; }
        };
        OkulSearchBox.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { OkulPopup.IsOpen = false; e.Handled = true; }
            else if (e.Key == Key.Enter && OkulOptionsList.Items.Count > 0) { OkulOptionsList.SelectedIndex = 0; e.Handled = true; }
        };
    }

    private void ApplyWin11WindowAttributes()
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            int dark = string.Equals(_settings.Tema, "koyu", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

            int cornerPref = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int));
        }
        catch { }
    }

    private Brush ResBrush(string key) => (Brush)Application.Current.Resources[key];

    // ── SİSTEM TEPSİSİ (GİZLİ SİMGELERDE LOGO GÖSTERİMİ) ───
    private void SetupSystemTray()
    {
        try
        {
            _trayIcon = new WinForms.NotifyIcon
            {
                Text = "e-Okul Kontenjan Takip"
            };

            // 1. Önce gömülü WPF kaynağından (pack://application:,,,/Assets/logo.ico) ikonu yükle
            System.Drawing.Icon? loadedIcon = null;
            try
            {
                var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/logo.ico"));
                if (streamInfo != null)
                {
                    using var s = streamInfo.Stream;
                    loadedIcon = new System.Drawing.Icon(s);
                }
            }
            catch { }

            // 2. Yedek: Çalışan EXE'nin kendi gömülü ikonu
            if (loadedIcon == null && !string.IsNullOrEmpty(Environment.ProcessPath))
            {
                try
                {
                    loadedIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                }
                catch { }
            }

            _trayIcon.Icon = loadedIcon ?? System.Drawing.SystemIcons.Application;
            _trayIcon.Visible = true;

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Pencereyi Göster", null, (_, _) => Dispatcher.Invoke(BringWindowToFront));
            menu.Items.Add("Gizle", null, (_, _) => Dispatcher.Invoke(Hide));
            menu.Items.Add("Şimdi Yenile", null, (_, _) => Dispatcher.Invoke(() => RefreshAllBtn_Click(this, new RoutedEventArgs())));
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("Çıkış", null, (_, _) => Dispatcher.Invoke(ExitApplication));

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.MouseClick += (_, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left)
                    Dispatcher.Invoke(BringWindowToFront);
            };
            _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(BringWindowToFront);
            _trayIcon.BalloonTipClicked += (_, _) => Dispatcher.Invoke(BringWindowToFront);
        }
        catch { }
    }

    public void BringWindowToFront()
    {
        try
        {
            if (!IsVisible)
                Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, 9); // SW_RESTORE
                SetForegroundWindow(hwnd);
            }
        }
        catch { }
    }

    private void ToggleWindowVisibility()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
        }
        else
        {
            BringWindowToFront();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_trayIcon is { Visible: true })
        {
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _trayIcon.ShowBalloonTip(
                    3000,
                    "e-Okul Kontenjan Takip",
                    "Uygulama arka planda (gizli simgelerde) çalışmaya devam ediyor.",
                    WinForms.ToolTipIcon.Info
                );
            }
            return;
        }
        base.OnClosing(e);
    }

    private void ExitApplication()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        App.ReleaseSingleInstance();
        Application.Current.Shutdown();
    }

    // ── SEKMELER VE TEMA ──────────────────────────
    private void NavTab_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewListPanel == null || ViewAddPanel == null || ViewSettingsPanel == null)
            return;

        ViewListPanel.Visibility = TabListBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewAddPanel.Visibility = TabAddBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewSettingsPanel.Visibility = TabSettingsBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GoToAddTab_Click(object sender, RoutedEventArgs e)
    {
        TabAddBtn.IsChecked = true;
    }

    private void ThemeToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        _settings.Tema = string.Equals(_settings.Tema, "koyu", StringComparison.OrdinalIgnoreCase) ? "acik" : "koyu";
        StorageService.SaveSettings(_settings);
        App.ApplyTheme(_settings.Tema);
        ApplyWin11WindowAttributes();
        SyncSettingsToUi();
        RenderGmCards();
        UpdateStepVisuals();
        RenderSchoolCards();
    }

    private bool _isDownloadingChromium;

    private void SyncSettingsToUi()
    {
        _suppressSettingEvents = true;
        try
        {
            bool isDark = string.Equals(_settings.Tema, "koyu", StringComparison.OrdinalIgnoreCase);
            ThemeToggleBtn.Content = isDark ? "Açık Tema" : "Koyu Tema";

            bool isChromium = string.Equals(_settings.SorguMotoru, "chromium", StringComparison.OrdinalIgnoreCase);
            ActiveEngineSubtitle.Text = isChromium ? "Chromium Tarayıcı" : "Düz İstek (HTTP / cURL)";

            EngineHttpCard.Background = !isChromium ? ResBrush("AccentSoftBrush") : ResBrush("CardInnerBrush");
            EngineHttpCard.BorderBrush = !isChromium ? ResBrush("AccentBrush") : ResBrush("BorderBrush");

            EngineChromiumCard.Background = isChromium ? ResBrush("AccentSoftBrush") : ResBrush("CardInnerBrush");
            EngineChromiumCard.BorderBrush = isChromium ? ResBrush("AccentBrush") : ResBrush("BorderBrush");

            UpdateChromiumInstallUi();

            foreach (ComboBoxItem item in IntervalComboBox.Items)
            {
                if (item.Tag?.ToString() == _settings.KontrolAraligi.ToString())
                {
                    IntervalComboBox.SelectedItem = item;
                    break;
                }
            }
            if (IntervalComboBox.SelectedItem == null)
                IntervalComboBox.SelectedIndex = 3;

            ChkWinNotify.IsChecked = _settings.BildirimWindows;
            ChkPopupNotify.IsChecked = _settings.BildirimPopup;
            ChkFlashNotify.IsChecked = _settings.BildirimFlash;
            ChkStartup.IsChecked = StartupService.IsRegistered();
            UpdateStartupPathUi();
        }
        finally
        {
            _suppressSettingEvents = false;
        }
    }

    private void UpdateStartupPathUi()
    {
        if (StartupPathText == null)
            return;

        string? target = StartupService.GetShortcutTarget();
        if (!string.IsNullOrEmpty(target) && StartupService.IsRegistered())
        {
            StartupPathText.Text = $"Klasör: {StartupService.StartupFolder}\nHedef EXE: {target} --minimized";
        }
        else
        {
            StartupPathText.Text = $"Klasör: {StartupService.StartupFolder} (Kısayol aktif değil)";
        }
    }

    private void UpdateChromiumInstallUi()
    {
        if (ChromiumPathText == null)
            return;

        ChromiumPathText.Text = $"Kurulum Yolu: {ChromiumInstallerService.ChromiumRootDir}";
        bool installed = ChromiumInstallerService.IsInstalled();

        if (installed)
        {
            ChromiumInstallBadge.Background = ResBrush("GreenSoftBrush");
            ChromiumInstallBadgeText.Text = "Kurulu • Hazır";
            ChromiumInstallBadgeText.Foreground = ResBrush("GreenBrush");
            if (!_isDownloadingChromium)
            {
                DownloadChromiumBtnText.Text = "Kurulu (Yeniden İndir)";
                DownloadChromiumBtn.Style = (Style)Application.Current.Resources["FluentButtonStyle"];
            }
        }
        else
        {
            ChromiumInstallBadge.Background = ResBrush("AmberSoftBrush");
            ChromiumInstallBadgeText.Text = "İndirme Gerekir (~105 MB)";
            ChromiumInstallBadgeText.Foreground = ResBrush("AmberBrush");
            if (!_isDownloadingChromium)
            {
                DownloadChromiumBtnText.Text = "Chromium'u İndir ve Kur";
                DownloadChromiumBtn.Style = (Style)Application.Current.Resources["PrimaryButtonStyle"];
            }
        }
    }

    // ── OKULLARIM KARTLARINI OLUŞTURMA ────────────
    private void RenderSchoolCards()
    {
        if (SchoolsStackPanel == null)
            return;

        SchoolCountBadgeText.Text = _data.Okullar.Count.ToString();
        SchoolsStackPanel.Children.Clear();

        if (_data.Okullar.Count == 0)
        {
            EmptyListCard.Visibility = Visibility.Visible;
            return;
        }
        EmptyListCard.Visibility = Visibility.Collapsed;

        for (int i = 0; i < _data.Okullar.Count; i++)
        {
            int schoolIdx = i;
            var school = _data.Okullar[i];
            _data.Sonuclar.TryGetValue(school.Key, out var res);
            bool isChecking = _activeChecks.Contains(school.Key);
            bool hasQuota = StorageService.HasAvailableQuota(res, school.Siniflar);

            var cardBorder = new Border
            {
                Background = ResBrush("SurfaceBrush"),
                BorderBrush = hasQuota ? ResBrush("GreenBorderBrush") : ResBrush("BorderBrush"),
                BorderThickness = new Thickness(hasQuota ? 1.5 : 1),
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 14)
            };

            var cardStack = new StackPanel();

            var topGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock
            {
                Text = school.Okul,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = ResBrush("TextPrimaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });

            var metaWrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            metaWrap.Children.Add(MakeMetaBadge($"{school.Ilce} / {school.Il}", false));

            var (tur, dil) = StorageService.ParseMeta(res);
            if (!string.IsNullOrEmpty(tur))
                metaWrap.Children.Add(MakeMetaBadge(tur, false));
            if (!string.IsNullOrEmpty(dil))
                metaWrap.Children.Add(MakeMetaBadge(dil, false));
            if (hasQuota)
                metaWrap.Children.Add(MakeMetaBadge("Boş Kontenjan Var", true));

            if (res != null && !string.IsNullOrEmpty(res.SonKontrol) && res.SonKontrol != "-")
            {
                metaWrap.Children.Add(new TextBlock
                {
                    Text = $"Son kontrol: {res.SonKontrol}",
                    FontSize = 11.5,
                    Foreground = ResBrush("TextMutedBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0)
                });
            }

            infoStack.Children.Add(metaWrap);
            Grid.SetColumn(infoStack, 0);
            topGrid.Children.Add(infoStack);

            var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            var refreshBtn = new Button
            {
                Content = isChecking ? "Sorgulanıyor..." : "Yenile",
                Style = (Style)Application.Current.Resources["FluentButtonStyle"],
                Padding = new Thickness(11, 5, 11, 5),
                FontSize = 12,
                IsEnabled = !isChecking,
                Margin = new Thickness(0, 0, 6, 0)
            };
            refreshBtn.Click += async (_, _) => await CheckSingleSchoolAsync(school, true);

            var removeBtn = new Button
            {
                Content = "Kaldır",
                Style = (Style)Application.Current.Resources["FluentButtonStyle"],
                Padding = new Thickness(11, 5, 11, 5),
                FontSize = 12,
                Foreground = ResBrush("DangerBrush")
            };
            removeBtn.Click += (_, _) => RemoveSchoolAt(schoolIdx);

            actionStack.Children.Add(refreshBtn);
            actionStack.Children.Add(removeBtn);
            Grid.SetColumn(actionStack, 1);
            topGrid.Children.Add(actionStack);

            cardStack.Children.Add(topGrid);

            string? errMsg = StorageService.GetErrorMessage(res);
            if (errMsg != null && res is { SiteKapali: false })
            {
                var errBorder = new Border
                {
                    Background = ResBrush("DangerSoftBrush"),
                    BorderBrush = ResBrush("DangerBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 8, 12, 8)
                };

                var errGrid = new Grid();
                errGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                errGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var errTxt = new TextBlock
                {
                    Text = errMsg,
                    Foreground = ResBrush("DangerBrush"),
                    FontSize = 12.5,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(errTxt, 0);
                errGrid.Children.Add(errTxt);

                if (errMsg.Contains("Chromium", StringComparison.OrdinalIgnoreCase) && !ChromiumInstallerService.IsInstalled())
                {
                    var dlBtn = new Button
                    {
                        Content = "Chromium'u İndir ve Kur",
                        Style = (Style)Application.Current.Resources["PrimaryButtonStyle"],
                        Padding = new Thickness(12, 5, 12, 5),
                        FontSize = 12,
                        Margin = new Thickness(10, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    dlBtn.Click += async (_, _) =>
                    {
                        TabSettingsBtn.IsChecked = true;
                        await StartChromiumDownloadAsync(true);
                    };
                    Grid.SetColumn(dlBtn, 1);
                    errGrid.Children.Add(dlBtn);
                }

                errBorder.Child = errGrid;
                cardStack.Children.Add(errBorder);
            }
            else
            {
                var gradesGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4 };
                foreach (var g in new[] { "9", "10", "11", "12" })
                {
                    string gradeStr = g;
                    bool isTracked = school.Siniflar.Contains(gradeStr);
                    var q = StorageService.ParseGrade(res, gradeStr);
                    bool avail = isTracked && !string.IsNullOrWhiteSpace(q.Kontenjan) &&
                                 q.Kontenjan != "0" && q.Kontenjan != "?" && q.Kontenjan != "-";

                    var gBox = new Border
                    {
                        Background = avail ? ResBrush("GreenSoftBrush") : ResBrush("CardInnerBrush"),
                        BorderBrush = avail ? ResBrush("GreenBorderBrush") : ResBrush("BorderBrush"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(12, 10, 12, 10),
                        Margin = new Thickness(0, 0, gradeStr == "12" ? 0 : 8, 0),
                        Opacity = isTracked ? 1.0 : 0.45,
                        Cursor = Cursors.Hand,
                        ToolTip = "Bu sınıfın bildirim takibini aç/kapatmak için tıklayın"
                    };

                    var gStack = new StackPanel();
                    var gHead = new Grid();
                    gHead.Children.Add(new TextBlock
                    {
                        Text = $"{gradeStr}. Sınıf",
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = avail ? ResBrush("GreenBrush") : ResBrush("TextSecondaryBrush")
                    });
                    gHead.Children.Add(new TextBlock
                    {
                        Text = isTracked ? "Takipte" : "Kapalı",
                        FontSize = 10.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ResBrush("TextMutedBrush"),
                        HorizontalAlignment = HorizontalAlignment.Right
                    });
                    gStack.Children.Add(gHead);

                    gStack.Children.Add(new TextBlock
                    {
                        Text = isChecking ? "..." : q.Kontenjan,
                        FontSize = 22,
                        FontWeight = FontWeights.ExtraBold,
                        Foreground = avail ? ResBrush("GreenBrush") : ResBrush("TextPrimaryBrush"),
                        Margin = new Thickness(0, 4, 0, 2)
                    });

                    gStack.Children.Add(new TextBlock
                    {
                        Text = !string.IsNullOrEmpty(q.Puan) ? $"Puan: {q.Puan}" : (avail ? "Boş kontenjan açık" : "Kontenjan"),
                        FontSize = 11,
                        Foreground = ResBrush("TextMutedBrush")
                    });

                    gBox.Child = gStack;
                    gBox.MouseLeftButtonUp += (_, _) => ToggleSchoolGrade(schoolIdx, gradeStr);
                    gradesGrid.Children.Add(gBox);
                }
                cardStack.Children.Add(gradesGrid);
            }

            cardBorder.Child = cardStack;
            SchoolsStackPanel.Children.Add(cardBorder);
        }
    }

    private Border MakeMetaBadge(string text, bool isGreen)
    {
        return new Border
        {
            Background = isGreen ? ResBrush("GreenSoftBrush") : ResBrush("CardInnerBrush"),
            BorderBrush = isGreen ? ResBrush("GreenBorderBrush") : ResBrush("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = isGreen ? ResBrush("GreenBrush") : ResBrush("TextSecondaryBrush")
            }
        };
    }

    private void ToggleSchoolGrade(int schoolIdx, string grade)
    {
        if (schoolIdx < 0 || schoolIdx >= _data.Okullar.Count)
            return;

        var s = _data.Okullar[schoolIdx];
        if (s.Siniflar.Contains(grade))
        {
            if (s.Siniflar.Count <= 1)
                return;
            s.Siniflar.Remove(grade);
        }
        else
        {
            s.Siniflar.Add(grade);
            s.Siniflar = s.Siniflar.OrderBy(int.Parse).ToList();
        }
        StorageService.SaveData(_data);
        RenderSchoolCards();
        AppendLog($"{s.Okul}: takip edilen sınıflar ({string.Join(", ", s.Siniflar)}. Sınıf) güncellendi.", "bilgi");
    }

    private void RemoveSchoolAt(int idx)
    {
        if (idx < 0 || idx >= _data.Okullar.Count)
            return;

        var s = _data.Okullar[idx];
        _data.Okullar.RemoveAt(idx);
        _data.Sonuclar.Remove(s.Key);
        StorageService.SaveData(_data);
        RenderSchoolCards();
        AppendLog($"Okul kaldırıldı: {s.Key}", "uyari");
    }

    // ── ADIM ADIM GÖRSEL REHBERLİ OKUL EKLEME ─────
    private void UpdateStepVisuals()
    {
        bool hasGm = !string.IsNullOrEmpty(_selectedGm);
        bool hasIl = !string.IsNullOrEmpty(_selectedIl);
        bool hasIlce = !string.IsNullOrEmpty(_selectedIlce);
        bool hasOkul = !string.IsNullOrEmpty(_selectedOkul);

        SetBadgeState(Step1Badge, Step1BadgeText, Step1CheckIcon, "1", hasGm, !hasGm);
        SetBadgeState(Step2Badge, Step2BadgeText, Step2CheckIcon, "2", hasIl, hasGm && !hasIl);
        SetBadgeState(Step3Badge, Step3BadgeText, Step3CheckIcon, "3", hasIlce, hasIl && !hasIlce);
        SetBadgeState(Step4Badge, Step4BadgeText, Step4CheckIcon, "4", hasOkul, hasIlce && !hasOkul);

        // Sırası gelen açılır kutunun çerçevesini mavi vurgula ki kullanıcı nereye tıklayacağını hemen anlasın
        HighlightActivePickerButton(IlPickerBtn, hasGm && !hasIl && IlPickerBtn.IsEnabled);
        HighlightActivePickerButton(IlcePickerBtn, hasIl && !hasIlce && IlcePickerBtn.IsEnabled);
        HighlightActivePickerButton(OkulPickerBtn, hasIlce && !hasOkul && OkulPickerBtn.IsEnabled);
    }

    private void SetBadgeState(Border badge, TextBlock txt, System.Windows.Shapes.Path checkIcon, string num, bool completed, bool active)
    {
        if (completed)
        {
            badge.Background = ResBrush("GreenBrush");
            txt.Visibility = Visibility.Collapsed;
            checkIcon.Visibility = Visibility.Visible;
        }
        else if (active)
        {
            badge.Background = ResBrush("AccentBrush");
            txt.Text = num;
            txt.Foreground = Brushes.White;
            txt.Visibility = Visibility.Visible;
            checkIcon.Visibility = Visibility.Collapsed;
        }
        else
        {
            badge.Background = ResBrush("BorderHoverBrush");
            txt.Text = num;
            txt.Foreground = ResBrush("TextPrimaryBrush");
            txt.Visibility = Visibility.Visible;
            checkIcon.Visibility = Visibility.Collapsed;
        }
    }

    private void HighlightActivePickerButton(Button btn, bool isNextStep)
    {
        if (isNextStep)
        {
            btn.BorderBrush = ResBrush("AccentBrush");
            btn.BorderThickness = new Thickness(1.5);
            btn.Background = ResBrush("AccentSoftBrush");
        }
        else
        {
            btn.BorderBrush = ResBrush("BorderBrush");
            btn.BorderThickness = new Thickness(1);
            btn.Background = ResBrush("SurfaceBrush");
        }
    }

    private void SetAddBanner(string message, string level = "")
    {
        AddStatusText.Text = message;
        if (level == "warn")
        {
            AddStatusBorder.Background = ResBrush("AmberSoftBrush");
            AddStatusBorder.BorderBrush = ResBrush("AmberBrush");
            AddStatusText.Foreground = ResBrush("AmberBrush");
        }
        else if (level == "error")
        {
            AddStatusBorder.Background = ResBrush("DangerSoftBrush");
            AddStatusBorder.BorderBrush = ResBrush("DangerBrush");
            AddStatusText.Foreground = ResBrush("DangerBrush");
        }
        else if (level == "ok")
        {
            AddStatusBorder.Background = ResBrush("GreenSoftBrush");
            AddStatusBorder.BorderBrush = ResBrush("GreenBorderBrush");
            AddStatusText.Foreground = ResBrush("GreenBrush");
        }
        else
        {
            AddStatusBorder.Background = ResBrush("CardInnerBrush");
            AddStatusBorder.BorderBrush = ResBrush("BorderBrush");
            AddStatusText.Foreground = ResBrush("TextSecondaryBrush");
        }
    }

    private void InitDefaultGmCardsOffline()
    {
        _selectedGm = "";
        _selectedIl = "";
        _selectedIlce = "";
        _selectedOkul = "";

        ResetPicker(IlPickerBtn, IlPickerBtnText, "Önce 1. adımdan lise türü seçin", false);
        ResetPicker(IlcePickerBtn, IlcePickerBtnText, "Önce 2. adımdan il seçin", false);
        ResetPicker(OkulPickerBtn, OkulPickerBtnText, "Önce 3. adımdan ilçe seçin", false);

        _gmOptions = CascadingDropdownSession.GetDefaultGmOptions();
        RenderGmCards();
        UpdateStepVisuals();
        SetAddBanner("1. adımdan aradığınız lise türünü (örn. Anadolu Liseleri) seçin.");
    }

    private async void ReloadGmBtn_Click(object sender, RoutedEventArgs e)
    {
        await LoadGmListAsync();
    }

    private async Task LoadGmListAsync()
    {
        _selectedGm = "";
        _selectedIl = "";
        _selectedIlce = "";
        _selectedOkul = "";

        ResetPicker(IlPickerBtn, IlPickerBtnText, "Önce 1. adımdan lise türü seçin", false);
        ResetPicker(IlcePickerBtn, IlcePickerBtnText, "Önce 2. adımdan il seçin", false);
        ResetPicker(OkulPickerBtn, OkulPickerBtnText, "Önce 3. adımdan ilçe seçin", false);
        UpdateStepVisuals();
        SetAddBanner("Okul türleri yükleniyor...");

        var (opts, err, closed) = await _dropdownSession.LoadGmAsync();
        if (closed)
        {
            SetAddBanner($"e-Okul şu anda kapalı: {err}", "warn");
            return;
        }
        if (err != null || opts.Count == 0)
        {
            SetAddBanner($"Hata: {err ?? "Okul türleri alınamadı."}", "error");
            return;
        }

        _gmOptions = opts;
        RenderGmCards();
        UpdateStepVisuals();
        SetAddBanner("1. adımdan aradığınız lise türünü (örn. Anadolu Liseleri) seçin.");
    }

    private void RenderGmCards()
    {
        if (GmCardsGrid == null)
            return;

        GmCardsGrid.Children.Clear();
        for (int i = 0; i < _gmOptions.Count; i++)
        {
            var opt = _gmOptions[i];
            bool selected = _selectedGm == opt.Text;

            var bdr = new Border
            {
                Background = selected ? ResBrush("AccentSoftBrush") : ResBrush("CardInnerBrush"),
                BorderBrush = selected ? ResBrush("AccentBrush") : ResBrush("BorderBrush"),
                BorderThickness = new Thickness(selected ? 1.5 : 1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, i % 3 == 2 ? 0 : 8, 8),
                Cursor = Cursors.Hand
            };

            var st = new StackPanel();
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            if (selected)
            {
                var checkIcon = UiIcons.CreateIcon(UiIcons.CheckPath, ResBrush("AccentBrush"), 11.5, 1.9);
                checkIcon.Margin = new Thickness(0, 0, 5, 0);
                titleRow.Children.Add(checkIcon);
            }
            titleRow.Children.Add(new TextBlock
            {
                Text = opt.ShortTitle,
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = selected ? ResBrush("AccentBrush") : ResBrush("TextPrimaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            st.Children.Add(titleRow);
            st.Children.Add(new TextBlock
            {
                Text = opt.Subtitle,
                FontSize = 10.5,
                Foreground = ResBrush("TextSecondaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            });

            bdr.Child = st;
            bdr.MouseLeftButtonUp += async (_, _) => await SelectGmOptionAsync(opt);
            GmCardsGrid.Children.Add(bdr);
        }
    }

    private async Task SelectGmOptionAsync(SelectOption opt)
    {
        _selectedGm = opt.Text;
        _selectedIl = "";
        _selectedIlce = "";
        _selectedOkul = "";
        RenderGmCards();

        ResetPicker(IlPickerBtn, IlPickerBtnText, "İller yükleniyor...", false);
        ResetPicker(IlcePickerBtn, IlcePickerBtnText, "Önce 2. adımdan il seçin", false);
        ResetPicker(OkulPickerBtn, OkulPickerBtnText, "Önce 3. adımdan ilçe seçin", false);
        UpdateStepVisuals();
        SetAddBanner($"{opt.ShortTitle} için iller yükleniyor...");

        var (ilOpts, err) = await _dropdownSession.LoadIlAsync(opt.Text);
        if (err != null || ilOpts.Count == 0)
        {
            SetAddBanner(err ?? "İller yüklenemedi.", "error");
            return;
        }

        _allIlOptions = ilOpts.Select(x => x.Text).ToList();
        ResetPicker(IlPickerBtn, IlPickerBtnText, "İl seçmek için tıklayın", true);
        UpdateStepVisuals();
        SetAddBanner($"2. adımdan İl kutusuna tıklayıp ilinizi seçin ({_allIlOptions.Count} il hazır).");
    }

    private static void ResetPicker(Button btn, TextBlock txt, string label, bool enabled)
    {
        txt.Text = label;
        btn.IsEnabled = enabled;
    }

    // ── İL ARAMALI SEÇİCİ ─────────────────────────
    private void IlPickerBtn_Click(object sender, RoutedEventArgs e)
    {
        IlSearchBox.Text = "";
        PopulateFilteredList(IlOptionsList, _allIlOptions, _selectedIl);
        IlPopup.IsOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => IlSearchBox.Focus());
    }

    private void IlSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string q = EOkulHttpSession.Normalize(IlSearchBox.Text);
        var filtered = string.IsNullOrEmpty(q)
            ? _allIlOptions
            : _allIlOptions.Where(x => EOkulHttpSession.Normalize(x).Contains(q)).ToList();
        PopulateFilteredList(IlOptionsList, filtered, _selectedIl);
    }

    private async void IlOptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressListSelection || IlOptionsList.SelectedItem is not string val)
            return;

        IlPopup.IsOpen = false;
        _selectedIl = val;
        _selectedIlce = "";
        _selectedOkul = "";
        IlPickerBtnText.Text = val;

        ResetPicker(IlcePickerBtn, IlcePickerBtnText, "İlçeler yükleniyor...", false);
        ResetPicker(OkulPickerBtn, OkulPickerBtnText, "Önce 3. adımdan ilçe seçin", false);
        UpdateStepVisuals();
        SetAddBanner($"{val} ilçeleri yükleniyor...");

        var (ilceOpts, err) = await _dropdownSession.LoadIlceAsync(_selectedGm, val);
        if (err != null || ilceOpts.Count == 0)
        {
            SetAddBanner(err ?? "İlçeler yüklenemedi.", "error");
            return;
        }

        _allIlceOptions = ilceOpts.Select(x => x.Text).ToList();
        ResetPicker(IlcePickerBtn, IlcePickerBtnText, "İlçe seçmek için tıklayın", true);
        UpdateStepVisuals();
        SetAddBanner($"3. adımdan İlçe kutusuna tıklayıp ilçeyi seçin ({_allIlceOptions.Count} ilçe hazır).");
    }

    // ── İLÇE ARAMALI SEÇİCİ ───────────────────────
    private void IlcePickerBtn_Click(object sender, RoutedEventArgs e)
    {
        IlceSearchBox.Text = "";
        PopulateFilteredList(IlceOptionsList, _allIlceOptions, _selectedIlce);
        IlcePopup.IsOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => IlceSearchBox.Focus());
    }

    private void IlceSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string q = EOkulHttpSession.Normalize(IlceSearchBox.Text);
        var filtered = string.IsNullOrEmpty(q)
            ? _allIlceOptions
            : _allIlceOptions.Where(x => EOkulHttpSession.Normalize(x).Contains(q)).ToList();
        PopulateFilteredList(IlceOptionsList, filtered, _selectedIlce);
    }

    private async void IlceOptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressListSelection || IlceOptionsList.SelectedItem is not string val)
            return;

        IlcePopup.IsOpen = false;
        _selectedIlce = val;
        _selectedOkul = "";
        IlcePickerBtnText.Text = val;

        ResetPicker(OkulPickerBtn, OkulPickerBtnText, "Okullar yükleniyor...", false);
        UpdateStepVisuals();
        SetAddBanner($"{val} ilçesindeki okullar yükleniyor...");

        var (okulOpts, err) = await _dropdownSession.LoadOkulAsync(_selectedGm, _selectedIl, val);
        if (err != null)
        {
            SetAddBanner(err, "error");
            return;
        }

        var (shortTur, _) = SelectOption.FormatGmTitle(_selectedGm);
        if (okulOpts.Count == 0)
        {
            string msg = $"{val} ilçesinde '{shortTur}' türünde kayıtlı lise bulunmuyor. Anadolu Lisesi arıyorsanız 1. adımdan 'Anadolu Liseleri' seçebilirsiniz.";
            SetAddBanner(msg, "warn");
            ResetPicker(OkulPickerBtn, OkulPickerBtnText, "Bu ilçede okul bulunamadı", false);
            UpdateStepVisuals();
            return;
        }

        _allOkulOptions = okulOpts.Select(x => x.Text).ToList();
        ResetPicker(OkulPickerBtn, OkulPickerBtnText, $"Okul seçmek için tıklayın ({_allOkulOptions.Count} Okul)", true);
        UpdateStepVisuals();
        SetAddBanner($"4. adımdan Okul kutusunu açıp lisenizi seçin ({_allOkulOptions.Count} okul bulundu).");
    }

    // ── OKUL ARAMALI SEÇİCİ ───────────────────────
    private void OkulPickerBtn_Click(object sender, RoutedEventArgs e)
    {
        OkulSearchBox.Text = "";
        PopulateFilteredList(OkulOptionsList, _allOkulOptions, _selectedOkul);
        OkulPopup.IsOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => OkulSearchBox.Focus());
    }

    private void OkulSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string q = EOkulHttpSession.Normalize(OkulSearchBox.Text);
        var filtered = string.IsNullOrEmpty(q)
            ? _allOkulOptions
            : _allOkulOptions.Where(x => EOkulHttpSession.Normalize(x).Contains(q)).ToList();
        PopulateFilteredList(OkulOptionsList, filtered, _selectedOkul);
    }

    private void OkulOptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressListSelection || OkulOptionsList.SelectedItem is not string val)
            return;

        OkulPopup.IsOpen = false;
        _selectedOkul = val;
        OkulPickerBtnText.Text = val;
        UpdateStepVisuals();
        SetAddBanner($"'{val}' seçildi. Aşağıdaki butona basarak takip listesine ekleyebilirsiniz.", "ok");
    }

    private void PopulateFilteredList(ListBox lb, List<string> items, string selectedVal)
    {
        _suppressListSelection = true;
        try
        {
            lb.ItemsSource = items;
            lb.SelectedItem = items.Contains(selectedVal) ? selectedVal : null;
        }
        finally
        {
            _suppressListSelection = false;
        }
    }

    private bool EnsureEulaAccepted()
    {
        if (_settings.EulaOnaylandi)
            return true;

        var eulaWin = new EulaWindow(readOnlyMode: false) { Owner = this };
        eulaWin.ShowDialog();
        if (!eulaWin.Accepted)
        {
            SetAddBanner("Okulu takip listesine eklemek ve sorgulama başlatmak için Kullanım Bilgilendirmesi'ni onaylamanız gerekir.", "warn");
            AppendLog("Kullanım Bilgilendirmesi (EULA) onaylanmadığı için işlem durduruldu.", "uyari");
            return false;
        }

        _settings.EulaOnaylandi = true;
        StorageService.SaveSettings(_settings);
        AppendLog("Kullanım Bilgilendirmesi (EULA) onaylandı.", "basari");
        return true;
    }

    private async void AddSchoolSubmitBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedGm))
        {
            SetAddBanner("Lütfen önce 1. adımdan bir Lise Türü (örn. Anadolu Liseleri) seçin.", "warn");
            return;
        }
        if (string.IsNullOrEmpty(_selectedIl))
        {
            SetAddBanner("Lütfen 2. adımdan İl seçin.", "warn");
            return;
        }
        if (string.IsNullOrEmpty(_selectedIlce))
        {
            SetAddBanner("Lütfen 3. adımdan İlçe seçin.", "warn");
            return;
        }
        if (string.IsNullOrEmpty(_selectedOkul))
        {
            SetAddBanner("Lütfen 4. adımdan Okul seçin.", "warn");
            return;
        }

        var grades = new List<string>();
        if (CbGrade9.IsChecked == true) grades.Add("9");
        if (CbGrade10.IsChecked == true) grades.Add("10");
        if (CbGrade11.IsChecked == true) grades.Add("11");
        if (CbGrade12.IsChecked == true) grades.Add("12");

        if (grades.Count == 0)
        {
            SetAddBanner("Bildirim almak için en az bir sınıf seçmelisiniz.", "warn");
            return;
        }

        if (_data.Okullar.Any(x => x.Gm == _selectedGm && x.Il == _selectedIl && x.Ilce == _selectedIlce && x.Okul == _selectedOkul))
        {
            SetAddBanner("Bu okul zaten takip listenizde mevcut. 'Okullarım' sekmesinden görebilirsiniz.", "warn");
            return;
        }

        // Okulu listeye eklemeden ve e-Okul / Chromium sorgusu başlatmadan önce EULA onayını kontrol et!
        if (!EnsureEulaAccepted())
            return;

        var newSchool = new TrackedSchool
        {
            Gm = _selectedGm,
            Il = _selectedIl,
            Ilce = _selectedIlce,
            Okul = _selectedOkul,
            Siniflar = grades
        };

        _data.Okullar.Add(newSchool);
        StorageService.SaveData(_data);
        RenderSchoolCards();
        TabListBtn.IsChecked = true;
        AppendLog($"Okul listeye eklendi: {newSchool.Okul}", "basari");

        await CheckSingleSchoolAsync(newSchool, false);
    }

    // ── KONTENJAN SORGULAMA VE BİLDİRİMLER ────────
    private async void RefreshAllBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureEulaAccepted())
            return;

        await CheckAllSchoolsAsync();
    }

    private async Task CheckAllSchoolsAsync()
    {
        if (!_settings.EulaOnaylandi || _isRefreshingAll || _data.Okullar.Count == 0)
            return;

        _isRefreshingAll = true;
        _countdownSeconds = _settings.KontrolAraligi > 0 ? _settings.KontrolAraligi : 3600;
        RefreshAllBtn.IsEnabled = false;
        RefreshAllBtn.Content = "Sorgulanıyor...";

        var list = _data.Okullar.ToList();
        AppendLog($"Tüm okullar sorgulanıyor ({list.Count} okul)...", "bilgi");

        foreach (var school in list)
        {
            await CheckSingleSchoolAsync(school, true);
        }

        _isRefreshingAll = false;
        RefreshAllBtn.IsEnabled = true;
        RefreshAllBtn.Content = "Şimdi Yenile";
        AppendLog("Tüm okulların kontrolü tamamlandı.", "basari");
    }

    private async Task CheckSingleSchoolAsync(TrackedSchool school, bool notifyIfChanged)
    {
        if (!EnsureEulaAccepted())
            return;

        if (_activeChecks.Contains(school.Key))
            return;

        _activeChecks.Add(school.Key);
        RenderSchoolCards();

        _data.Sonuclar.TryGetValue(school.Key, out var oldRes);
        AppendLog($"{school.Key}: kontenjan sorgulanıyor...", "bilgi");

        var newRes = await EOkulQuotaService.CheckSchoolQuotaAsync(school, _settings.SorguMotoru);
        _activeChecks.Remove(school.Key);

        if (newRes.SiteKapali && oldRes != null)
        {
            oldRes.SiteKapali = true;
            oldRes.SiteKapaliMesaji = newRes.SiteKapaliMesaji;
            oldRes.SonKontrol = newRes.SonKontrol;
            AppendLog($"{school.Key}: e-Okul kapalı, önceki veri korundu.", "uyari");
        }
        else
        {
            var changedGrades = new List<string>();
            if (notifyIfChanged && oldRes != null && StorageService.GetErrorMessage(oldRes) == null && StorageService.GetErrorMessage(newRes) == null)
            {
                foreach (var g in school.Siniflar)
                {
                    var oldQ = StorageService.ParseGrade(oldRes, g);
                    var newQ = StorageService.ParseGrade(newRes, g);
                    if (oldQ.Kontenjan != newQ.Kontenjan)
                    {
                        changedGrades.Add($"{g}. Sınıf: {newQ.Kontenjan} Boş Kontenjan");
                    }
                }
            }

            _data.Sonuclar[school.Key] = newRes;
            string? err = StorageService.GetErrorMessage(newRes);
            if (err != null)
            {
                AppendLog($"{school.Key}: Hata — {err}", "hata");
            }
            else
            {
                var parts = new[] { "9", "10", "11", "12" }
                    .Select(g => $"{g}.Sınıf: {StorageService.ParseGrade(newRes, g).Kontenjan}");
                AppendLog($"{school.Key}: Tamamlandı ({string.Join(" | ", parts)})", "basari");
            }

            if (changedGrades.Count > 0)
            {
                TriggerNotifications($"{school.Key} — Kontenjan Değişti!", string.Join("\n", changedGrades));
            }
        }

        StorageService.SaveData(_data);
        RenderSchoolCards();
    }

    private void TriggerNotifications(string title, string message)
    {
        AppendLog($"Bildirim gönderildi: {title}", "basari");

        // 1. Windows Sistem Bildirimi
        if (_settings.BildirimWindows && _trayIcon is { Visible: true })
        {
            try
            {
                _trayIcon.ShowBalloonTip(6000, title, message, WinForms.ToolTipIcon.Info);
            }
            catch { }
        }

        // 2. Uygulama İçi Uyarı Penceresi
        if (_settings.BildirimPopup)
        {
            var popupWin = new PopupAlertWindow(title, message);
            popupWin.Show();
        }

        // 3. Sesli ve Yanıp Sönen Acil Uyarı Penceresi (Gerçek yanıp sönen + tekrarlı sesli pencere!)
        if (_settings.BildirimFlash)
        {
            var flashWin = new FlashAlertWindow(title, message);
            flashWin.Show();
        }
    }

    // ── AYARLAR OLAYLARI ──────────────────────────
    private void EngineHttpCard_Click(object sender, MouseButtonEventArgs e)
    {
        _settings.SorguMotoru = "http";
        StorageService.SaveSettings(_settings);
        SyncSettingsToUi();
        AppendLog("Sorgu yöntemi 'Düz İstek (HTTP / cURL)' olarak ayarlandı.", "basari");
    }

    private async void EngineChromiumCard_Click(object sender, MouseButtonEventArgs e)
    {
        _settings.SorguMotoru = "chromium";
        StorageService.SaveSettings(_settings);
        SyncSettingsToUi();

        if (!ChromiumInstallerService.IsInstalled())
        {
            AppendLog("Chromium tarayıcı kurulu değil; %LOCALAPPDATA%\\E-Okul Checker\\chromium klasörüne indirme başlatılıyor...", "bilgi");
            await StartChromiumDownloadAsync(true);
        }
        else
        {
            AppendLog("Sorgu yöntemi 'Chromium Tarayıcı' olarak ayarlandı (Kurulu).", "basari");
        }
    }

    private async void DownloadChromiumBtn_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        await StartChromiumDownloadAsync(true);
    }

    private void OpenChromiumFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ChromiumInstallerService.OpenChromiumFolder();
    }

    private async Task StartChromiumDownloadAsync(bool autoRecheck)
    {
        if (_isDownloadingChromium)
            return;

        _isDownloadingChromium = true;
        DownloadChromiumBtn.IsEnabled = false;
        DownloadChromiumBtnText.Text = "İndiriliyor...";
        ChromiumProgressPanel.Visibility = Visibility.Visible;
        ChromiumProgressBar.Value = 0;
        ChromiumProgressLabel.Text = "Chromium indirme bağlantısı hazırlanıyor...";

        var (ok, msg) = await ChromiumInstallerService.DownloadAndInstallAsync((pct, dlMb, totMb, status) =>
        {
            Dispatcher.Invoke(() =>
            {
                ChromiumProgressBar.Value = pct;
                ChromiumProgressLabel.Text = status;
                BottomStatusText.Text = status;
            });
        });

        _isDownloadingChromium = false;
        DownloadChromiumBtn.IsEnabled = true;

        if (ok)
        {
            _settings.SorguMotoru = "chromium";
            StorageService.SaveSettings(_settings);
            SyncSettingsToUi();
            RenderSchoolCards();
            AppendLog($"Chromium tarayıcı başarıyla kuruldu: {ChromiumInstallerService.ChromiumRootDir}", "basari");

            if (autoRecheck && _data.Okullar.Count > 0)
            {
                await CheckAllSchoolsAsync();
            }
        }
        else
        {
            ChromiumProgressLabel.Text = msg;
            ChromiumProgressLabel.Foreground = ResBrush("DangerBrush");
            UpdateChromiumInstallUi();
            AppendLog(msg, "hata");
        }
    }

    private void IntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSettingEvents || IntervalComboBox.SelectedItem is not ComboBoxItem item)
            return;

        if (int.TryParse(item.Tag?.ToString(), out int sec) && sec > 0)
        {
            _settings.KontrolAraligi = sec;
            _countdownSeconds = sec;
            StorageService.SaveSettings(_settings);
            AppendLog($"Otomatik kontrol sıklığı {sec / 60} dakika olarak güncellendi.", "bilgi");
        }
    }

    private void SettingCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressSettingEvents)
            return;

        _settings.BildirimWindows = ChkWinNotify.IsChecked == true;
        _settings.BildirimPopup = ChkPopupNotify.IsChecked == true;
        _settings.BildirimFlash = ChkFlashNotify.IsChecked == true;
        StorageService.SaveSettings(_settings);
    }

    private void ChkStartup_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressSettingEvents)
            return;

        if (ChkStartup.IsChecked == true)
        {
            bool ok = StartupService.Register();
            UpdateStartupPathUi();
            AppendLog(ok
                ? $"Windows başlangıç kısayolu oluşturuldu: {StartupService.CurrentExePath}"
                : "Başlangıç kısayolu oluşturulamadı.", ok ? "basari" : "hata");
        }
        else
        {
            StartupService.Unregister();
            UpdateStartupPathUi();
            AppendLog("Windows başlangıç kısayolu kaldırıldı.", "bilgi");
        }
    }

    private void OpenStartupFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        StartupService.OpenStartupFolder();
    }

    private void TestNotificationBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_settings.BildirimWindows && !_settings.BildirimPopup && !_settings.BildirimFlash)
        {
            MessageBox.Show(this, "Test etmek için yukarıdaki bildirim yöntemlerinden en az birini işaretleyin.", "Bildirim Seçilmedi", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        TriggerNotifications("Kipa 10. Yıl Anadolu Lisesi / GAZİEMİR", "10. Sınıf: 2 Boş Kontenjan Açıldı!");
    }

    private void OpenAppDataFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        StorageService.OpenAppDataFolder();
    }

    private void ShowEulaBtn_Click(object sender, RoutedEventArgs e)
    {
        var eulaWin = new EulaWindow(readOnlyMode: true) { Owner = this };
        eulaWin.ShowDialog();
    }

    private void ResetAppBtn_Click(object sender, RoutedEventArgs e)
    {
        var confirmWin = new ConfirmResetWindow { Owner = this };
        confirmWin.ShowDialog();
        if (!confirmWin.Confirmed)
            return;

        // Logları silmeden okulları ve EULA onayını sıfırla
        StorageService.ResetDataAndSettingsKeepLogs();

        try
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
        catch { }

        App.ReleaseSingleInstance();

        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                exePath = StartupService.PermanentExePath;

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--restarting",
                    UseShellExecute = true
                });
            }
        }
        catch { }

        Environment.Exit(0);
    }

    // ── ZAMANLAYICI VE GÜNLÜK ─────────────────────
    private async void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        if (_isRefreshingAll)
            return;

        _countdownSeconds--;
        if (_countdownSeconds <= 0)
        {
            _countdownSeconds = _settings.KontrolAraligi > 0 ? _settings.KontrolAraligi : 3600;
            if (_settings.EulaOnaylandi && _data.Okullar.Count > 0)
            {
                await CheckAllSchoolsAsync();
            }
        }

        int m = Math.Max(0, _countdownSeconds) / 60;
        int s = Math.Max(0, _countdownSeconds) % 60;
        TimerBadgeText.Text = $"{m:00}:{s:00}";
    }

    private void ToggleLogBtn_Click(object sender, RoutedEventArgs e)
    {
        bool show = LogDrawerPanel.Visibility != Visibility.Visible;
        LogDrawerPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ToggleLogChevron.Data = Geometry.Parse(show ? UiIcons.ChevronDownPath : UiIcons.ChevronUpPath);
    }

    private void ClearLogsBtn_Click(object sender, RoutedEventArgs e)
    {
        LogTextBox.Clear();
        try
        {
            if (File.Exists(StorageService.LogFile))
                File.WriteAllText(StorageService.LogFile, string.Empty);
        }
        catch { }
    }

    private void ExportLogsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LogTextBox.Text))
            return;

        string path = Path.Combine(StorageService.AppDataDir, $"olay_gunlugu_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllText(path, LogTextBox.Text);
        MessageBox.Show(this, $"İşlem kayıtları kaydedildi:\n{path}", "Kaydedildi", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadPersistedLogs()
    {
        try
        {
            if (!File.Exists(StorageService.LogFile))
                return;

            var lines = File.ReadAllLines(StorageService.LogFile);
            var tail = lines.Skip(Math.Max(0, lines.Length - 150)).ToArray();
            if (tail.Length > 0)
            {
                LogTextBox.Text = string.Join(Environment.NewLine, tail) + Environment.NewLine;
                LogTextBox.ScrollToEnd();
            }
        }
        catch { }
    }

    private void AppendLog(string message, string level)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        BottomStatusText.Text = message;
        LogTextBox.AppendText(line + Environment.NewLine);
        LogTextBox.ScrollToEnd();

        try
        {
            string diskLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level.ToUpperInvariant()}] {message}" + Environment.NewLine;
            File.AppendAllText(StorageService.LogFile, diskLine);
        }
        catch { }
    }
}
