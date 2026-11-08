using System;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace EOkulKontenjanTakip;

/// <summary>
/// Uygulama içi standart bildirim penceresi.
/// </summary>
public class PopupAlertWindow : Window
{
    public PopupAlertWindow(string title, string message)
    {
        Title = "Kontenjan Bildirimi";
        Width = 410;
        Height = 235;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Background = (Brush)Application.Current.Resources["SurfaceBrush"];
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        SourceInitialized += (_, _) => App.ApplyWindowDwmAttributes(this);

        var root = new Grid { Margin = new Thickness(22, 18, 22, 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
        var bellIcon = UiIcons.CreateIcon(UiIcons.BellPath, (Brush)Application.Current.Resources["AccentBrush"], 16, 1.8);
        bellIcon.Margin = new Thickness(0, 0, 8, 0);
        headerRow.Children.Add(bellIcon);
        headerRow.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 330,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetRow(headerRow, 0);
        root.Children.Add(headerRow);

        var card = new Border
        {
            Background = (Brush)Application.Current.Resources["CardInnerBrush"],
            BorderBrush = (Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 12, 0, 14)
        };
        card.Child = new TextBlock
        {
            Text = message,
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(card, 1);
        root.Children.Add(card);

        var closeBtn = new Button
        {
            Content = "Tamam",
            Style = (Style)Application.Current.Resources["PrimaryButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(20, 7, 20, 7)
        };
        closeBtn.Click += (_, _) => Close();
        Grid.SetRow(closeBtn, 2);
        root.Children.Add(closeBtn);

        Content = root;
    }
}

/// <summary>
/// Kullanıcı kapatana kadar yanıp sönen ve düzenli aralıklarla ses çalan acil uyarı penceresi.
/// </summary>
public class FlashAlertWindow : Window
{
    private readonly DispatcherTimer _flashTimer;
    private readonly DispatcherTimer _soundTimer;
    private readonly Border _outerBorder;
    private readonly Border _innerCard;
    private bool _flashState;
    private bool _isClosed;

    public FlashAlertWindow(string title, string message)
    {
        Title = "Acil Kontenjan Uyarısı";
        Width = 440;
        Height = 275;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Background = (Brush)Application.Current.Resources["SurfaceBrush"];
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        SourceInitialized += (_, _) => App.ApplyWindowDwmAttributes(this);

        _outerBorder = new Border
        {
            BorderThickness = new Thickness(3),
            BorderBrush = (Brush)Application.Current.Resources["GreenBrush"],
            Background = (Brush)Application.Current.Resources["SurfaceBrush"],
            Padding = new Thickness(20, 16, 20, 16)
        };

        var stack = new StackPanel();

        var badgeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var alertIcon = UiIcons.CreateIcon(UiIcons.BellPath, (Brush)Application.Current.Resources["GreenBrush"], 15, 1.9);
        alertIcon.Margin = new Thickness(0, 0, 7, 0);
        badgeRow.Children.Add(alertIcon);
        badgeRow.Children.Add(new TextBlock
        {
            Text = "BOŞ KONTENJAN TESPİT EDİLDİ",
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = (Brush)Application.Current.Resources["GreenBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(badgeRow);

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(titleBlock);

        _innerCard = new Border
        {
            Background = (Brush)Application.Current.Resources["GreenSoftBrush"],
            BorderBrush = (Brush)Application.Current.Resources["GreenBorderBrush"],
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 16)
        };
        _innerCard.Child = new TextBlock
        {
            Text = message,
            FontSize = 14.5,
            FontWeight = FontWeights.ExtraBold,
            Foreground = (Brush)Application.Current.Resources["GreenBrush"],
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(_innerCard);

        var stopBtn = new Button
        {
            Content = "Uyarıyı ve Sesi Kapat",
            Style = (Style)Application.Current.Resources["PrimaryButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 9, 14, 9)
        };
        stopBtn.Click += (_, _) => Close();
        stack.Children.Add(stopBtn);

        _outerBorder.Child = stack;
        Content = _outerBorder;

        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        _flashTimer.Tick += (_, _) => ToggleFlash();
        _flashTimer.Start();

        _soundTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2400) };
        _soundTimer.Tick += (_, _) => PlayAlertSound();
        _soundTimer.Start();

        PlayAlertSound();

        Closed += (_, _) =>
        {
            _isClosed = true;
            _flashTimer.Stop();
            _soundTimer.Stop();
        };
    }

    private void ToggleFlash()
    {
        if (_isClosed)
            return;

        _flashState = !_flashState;
        if (_flashState)
        {
            _outerBorder.Background = (Brush)Application.Current.Resources["GreenSoftBrush"];
            _outerBorder.BorderBrush = (Brush)Application.Current.Resources["GreenBrush"];
        }
        else
        {
            _outerBorder.Background = (Brush)Application.Current.Resources["SurfaceBrush"];
            _outerBorder.BorderBrush = (Brush)Application.Current.Resources["AmberBrush"];
        }
    }

    private void PlayAlertSound()
    {
        if (_isClosed)
            return;

        try
        {
            SystemSounds.Exclamation.Play();
            Task.Run(() =>
            {
                try
                {
                    Console.Beep(880, 160);
                    Console.Beep(1046, 200);
                }
                catch { }
            });
        }
        catch { }
    }
}

/// <summary>
/// Okul ekleme / sorgulama öncesinde veya Ayarlar'dan açılan Kullanım Bilgilendirmesi (EULA) penceresi.
/// </summary>
public class EulaWindow : Window
{
    public bool Accepted { get; private set; }
    private bool _decisionMade;

    public static string GetEulaText() =>
        "Bu program, Millî Eğitim Bakanlığı'nın resmi bir uygulaması DEĞİLDİR; " +
        "bağımsız/kişisel bir yardımcı araçtır ve MEB ile herhangi bir kurumsal " +
        "bağlantısı yoktur.\n\n" +
        "Programı kullanmaya devam ederek aşağıdakileri okuyup anladığını " +
        "kabul etmiş olursun:\n\n" +
        "1) Bu program, senin belirlediğin sıklıkla (varsayılan: saatte bir) " +
        "https://e-okul.meb.gov.tr adresindeki HERKESE AÇIK kontenjan sorgulama " +
        "sayfasını senin adına otomatik olarak ziyaret eder ve seçtiğin " +
        "okul(lar) için boş kontenjan bilgisini okur.\n\n" +
        "2) Bu işlem için hiçbir kullanıcı adı, şifre, e-Devlet girişi ya da " +
        "T.C. kimlik bilgisi kullanılmaz veya istenmez; yalnızca herkesin " +
        "tarayıcıdan erişebildiği genel/herkese açık sayfa okunur.\n\n" +
        "3) Eklediğin okullar ve okunan kontenjan sonuçları YALNIZCA kendi " +
        "bilgisayarında (%APPDATA%\\E-Okul Checker klasöründe) saklanır; " +
        "hiçbir veri üçüncü bir sunucuya, geliştiriciye ya da başka bir " +
        "kişiye gönderilmez.\n\n" +
        "4) e-Okul sunucusuna gönderilen otomatik isteklerin sonucundan ve " +
        "kullanım şekillerinden (kontrol aralığını çok kısa tutmak, çok " +
        "sayıda okulu aynı anda takip etmek gibi) doğabilecek her türlü " +
        "sonuç (ör. sunucunun erişimi geçici olarak sınırlaması) kullanıcının " +
        "kendi sorumluluğundadır.\n\n" +
        "5) Program 'olduğu gibi', hiçbir garanti verilmeksizin sunulur. " +
        "Kontenjan bilgisinin doğruluğu ve güncelliği için her zaman MEB'in " +
        "resmi e-Okul sistemi esas alınmalıdır; bu programın gösterdiği " +
        "sonuçlar sadece bir hatırlatma/takip aracıdır, resmi bir belge " +
        "veya başvuru sonucu yerine geçmez.\n\n" +
        "Devam etmek için 'Kabul Ediyorum'a, programı kapatmak için 'Reddet ve Çık'a basın.";

    public EulaWindow(bool readOnlyMode = false)
    {
        Title = "Kullanım Bilgilendirmesi (EULA)";
        Width = 600;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Background = (Brush)Application.Current.Resources["BgBrush"];
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        SourceInitialized += (_, _) => App.ApplyWindowDwmAttributes(this);

        if (!readOnlyMode)
        {
            Closing += (_, e) =>
            {
                if (!_decisionMade)
                {
                    e.Cancel = true;
                }
            };
        }

        var root = new Grid { Margin = new Thickness(22, 18, 22, 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Üst Başlık (Vektörel Doküman Çizimi ile)
        var headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        var docIcon = UiIcons.CreateIcon(UiIcons.DocumentPath, (Brush)Application.Current.Resources["AccentBrush"], 17, 1.8);
        docIcon.Margin = new Thickness(0, 0, 8, 0);
        titleRow.Children.Add(docIcon);
        titleRow.Children.Add(new TextBlock
        {
            Text = "Kullanım Bilgilendirmesi ve Sorumluluk Beyanı",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        headerStack.Children.Add(titleRow);

        headerStack.Children.Add(new TextBlock
        {
            Text = readOnlyMode
                ? "Kullanım bilgilendirmesi metnini aşağıdan inceleyebilirsiniz."
                : "Okulu takip listesine eklemek ve kontenjan sorgulaması başlatmak için bu koşulları onaylamanız gerekmektedir.",
            FontSize = 12.5,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetRow(headerStack, 0);
        root.Children.Add(headerStack);

        // Sözleşme Metin Kutusu
        var textCard = new Border
        {
            Background = (Brush)Application.Current.Resources["SurfaceBrush"],
            BorderBrush = (Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var bodyText = new TextBlock
        {
            Text = GetEulaText(),
            FontSize = 13,
            LineHeight = 20,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            TextWrapping = TextWrapping.Wrap
        };
        scroll.Content = bodyText;
        textCard.Child = scroll;
        Grid.SetRow(textCard, 1);
        root.Children.Add(textCard);

        // Alt Butonlar (Emojisiz, vektörel çizimli)
        var footerGrid = new Grid();
        if (readOnlyMode)
        {
            var closeBtn = new Button
            {
                Content = "Kapat",
                Style = (Style)Application.Current.Resources["PrimaryButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(24, 8, 24, 8)
            };
            closeBtn.Click += (_, _) =>
            {
                Accepted = true;
                Close();
            };
            footerGrid.Children.Add(closeBtn);
        }
        else
        {
            var rejectBtn = new Button
            {
                Content = "Reddet ve Çık",
                Style = (Style)Application.Current.Resources["DangerButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(18, 8, 18, 8)
            };
            rejectBtn.Click += (_, _) =>
            {
                Accepted = false;
                _decisionMade = true;
                Close();
            };

            var acceptBtn = new Button
            {
                Content = UiIcons.IconText(UiIcons.CheckPath, "Kabul Ediyorum", Brushes.White, 13, 2.0),
                Style = (Style)Application.Current.Resources["PrimaryButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(20, 8, 20, 8)
            };
            acceptBtn.Click += (_, _) =>
            {
                Accepted = true;
                _decisionMade = true;
                Close();
            };

            footerGrid.Children.Add(rejectBtn);
            footerGrid.Children.Add(acceptBtn);
        }

        Grid.SetRow(footerGrid, 2);
        root.Children.Add(footerGrid);

        Content = root;
    }
}

/// <summary>
/// Programı Sıfırla onay penceresi (Loglar korunur, okullar + EULA sıfırlanır ve yeniden başlar).
/// </summary>
public class ConfirmResetWindow : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmResetWindow()
    {
        Title = "Programı Sıfırla ve Yeniden Başlat";
        Width = 460;
        Height = 275;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Background = (Brush)Application.Current.Resources["SurfaceBrush"];
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        SourceInitialized += (_, _) => App.ApplyWindowDwmAttributes(this);

        var root = new Grid { Margin = new Thickness(22, 18, 22, 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
        var resetIcon = UiIcons.CreateIcon(UiIcons.RefreshPath, (Brush)Application.Current.Resources["DangerBrush"], 16, 1.8);
        resetIcon.Margin = new Thickness(0, 0, 8, 0);
        headerRow.Children.Add(resetIcon);
        headerRow.Children.Add(new TextBlock
        {
            Text = "Programı Sıfırlamak İstiyor musunuz?",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetRow(headerRow, 0);
        root.Children.Add(headerRow);

        var infoCard = new Border
        {
            Background = (Brush)Application.Current.Resources["CardInnerBrush"],
            BorderBrush = (Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 12, 0, 14)
        };
        infoCard.Child = new TextBlock
        {
            Text = "• Takip listesindeki tüm okullar ve sonuç önbelleği silinecek.\n" +
                   "• Kullanım Bilgilendirmesi (EULA) onayı ve ayarlar sıfırlanacak.\n" +
                   "• İşlem kayıtları (loglar) SİLİNMEYECEK, olduğu gibi korunacak.\n\n" +
                   "Onayladığınızda uygulama temiz şekilde yeniden başlatılacaktır.",
            FontSize = 12.5,
            LineHeight = 19,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(infoCard, 1);
        root.Children.Add(infoCard);

        var footer = new Grid();
        var cancelBtn = new Button
        {
            Content = "Vazgeç",
            Style = (Style)Application.Current.Resources["FluentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(18, 7, 18, 7)
        };
        cancelBtn.Click += (_, _) =>
        {
            Confirmed = false;
            Close();
        };

        var confirmBtn = new Button
        {
            Content = UiIcons.IconText(UiIcons.RefreshPath, "Sıfırla ve Yeniden Başlat", (Brush)Application.Current.Resources["DangerBrush"], 13, 1.8),
            Style = (Style)Application.Current.Resources["DangerButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(16, 7, 16, 7)
        };
        confirmBtn.Click += (_, _) =>
        {
            Confirmed = true;
            Close();
        };

        footer.Children.Add(cancelBtn);
        footer.Children.Add(confirmBtn);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
    }
}
