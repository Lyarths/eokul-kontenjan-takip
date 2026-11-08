# 🎓 e-Okul Boş Kontenjan Takip Uygulaması

Lise nakil dönemlerinde her saat başı MEB e-Okul sayfasına girip tek tek **İl → İlçe → Okul** seçerek *"Acaba boş kontenjan açıldı mı?"* diye kontrol etme zahmetini ortadan kaldıran, **Windows 11 Fluent** tasarımlı modern masaüstü uygulaması.

---

## 💡 Ana Çalışma Mantığı: Siz Oyununuzu Oynarken O Nöbet Tutar!

Nakil dönemlerinde popüler liselerde açılan **1 kişilik** boş kontenjan bile dakikalar içinde fark edilmezse kaçabiliyor. Bu programın temel amacı **sizin yerinize 7/24 arka planda nöbet tutmaktır:**

1. **Bir Kere Ekleyin, Unutun:** İstediğiniz liseleri ve sadece ilgilendiğiniz sınıfları (`9, 10, 11 veya 12. Sınıf`) listeye ekleyin.
2. **Gizli Simgelerde (System Tray) Sessizce Çalışsın:** Uygulamayı sağ üstteki `X` tuşundan kapattığınızda program kapanmaz; sağ alttaki **Gizli Simgeler (Sistem Tepsisi)** bölümüne küçülür.
3. **Neredeyse Sıfır RAM ve CPU Tüketimi:** Özellikle varsayılan **Düz İstek (HTTP / cURL)** modunda arka planda hiçbir ağır tarayıcı çalıştırmaz. Siz tam ekran **oyununuzu oynarken, video izlerken veya gündelik işlerinizi yaparken** FPS düşüşü ya da RAM kullanımı yaratmadan belirlediğiniz sürede bir (örneğin her 30 veya 60 dakikada bir) saniyeler içinde e-Okul'u kontrol eder.
4. **Boş Kontenjan Açıldığı Anda Sizi Uyandırır:** Takip ettiğiniz bir okulun seçili sınıfında boş kontenjan açıldığı anda (`0` → `1+`):
   - Yeşil renkle kontenjanı vurgular,
   - Windows bildirim çubuğundan uyarı gönderir,
   - **Sesli ve Yanıp Sönen Acil Uyarı** özelliği açıksa siz kapatana kadar ekranınızda yanıp sönen bir alarm penceresi açar ve sesli uyarı verir. Böylece bilgisayar başında olmasanız bile kontenjanı kaçırmazsınız!

---

## ✨ Öne Çıkan Özellikler

### ⚡ 1. Çift Sorgu Motoru (Düz HTTP & Chromium Tarayıcı)
- **Düz İstek (HTTP / cURL) *(Önerilen)*:** Hiçbir ek kurulum veya tarayıcı gerektirmeden doğrudan MEB e-Okul form akışını (ViewState) taklit eder. Çok hızlıdır ve neredeyse hiç RAM harcamaz.
- **Chromium Tarayıcı Modu:** İhtiyaç duyulduğunda **Ayarlar** sekmesinden tek tıkla seçilebilir. Bilgisayarınızda Chromium yoksa uygulama kendi içinden **canlı indirme çubuğuyla (`İndirilen MB / Toplam MB`)** `%LOCALAPPDATA%\E-Okul Checker\chromium` klasörüne otomatik indirip kurar.

### 🎯 2. Sınıf Bazlı Akıllı Filtreleme
- Bir okulu eklerken veya sonradan **Okullarım** sekmesindeyken `9. Sınıf`, `10. Sınıf`, `11. Sınıf` ve `12. Sınıf` kutularına tıklayarak sadece bildirim almak istediğiniz sınıfları seçebilirsiniz.
- Örneğin sadece **10. Sınıf** seçiliyse, o okulun 9. sınıfında kontenjan açılsa bile sizi boşuna rahatsız etmez.

### 🔔 3. Üç Farklı Bildirim Seçeneği
- **Windows Bildirimleri:** Sağ alttan standart Windows sistem bildirimi çıkarır.
- **Uygulama İçi Uyarı Penceresi:** Hangi okulda ve hangi sınıfta kaç kişilik yer açıldığını listeleyen özet pencere açar.
- **Sesli ve Yanıp Sönen Acil Uyarı:** Kontenjan açıldığında siz **"Gördüm, Alarmı Kapat"** diyene kadar her 2.5 saniyede bir sesli uyarı çalar ve dikkat çekici şekilde yanıp söner.

### 🛡️ 4. Kullanıcı Dostu İnce Detaylar
- **Çift Açılma Koruması (Single-Instance):** Program arka planda veya gizli simgelerde açıkken `.exe` dosyasına tekrar tıklarsanız ikinci bir program açılmaz; gizli simgedeki mevcut pencere otomatik olarak ekranın önüne gelir.
- **Bozulmayan "Bilgisayar Açıldığında Başlat" Sistemi:** Ayarlardan başlangıçta çalıştırmayı açtığınızda uygulama kendi kopyasını `%LOCALAPPDATA%\E-Okul Checker\bin` altına güvenceye alır. Masaüstündeki `.exe` dosyasının yerini değiştirseniz veya silseniz bile başlangıç kısayolu bozulmaz.
- **Açık / Koyu Tema & Canlı İşlem Kayıtları:** Tek tıkla gece/gündüz teması geçişi ve alt barda açılıp kapanabilen detaylı işlem geçmişi (`Dosyaya Kaydet` destekli).

---

## 🚀 Nasıl Kullanılır?

1. Uygulamayı açın ve üst menüden **Okul Ekle** sekmesine geçin.
2. **1. Lise Türünü Seçin** (Anadolu, Fen ve Sosyal Bilimler, Mesleki ve Teknik, İmam Hatip, Güzel Sanatlar ve Spor).
3. **2. İl** ve **3. İlçe** kutularından seçim yapın *(kutular açıldığında klavyeyle doğrudan yazarak arayabilirsiniz)*.
4. **4. Okul Seçimi** kutusundan istediğiniz liseyi seçin, bildirim almak istediğiniz sınıfları işaretleyip **"Takip Listesine Ekle ve Şimdi Sorgula"** butonuna basın.
5. Artık pencereyi kapatıp gündelik işlerinize veya oyununuza dönebilirsiniz; program sağ alt tepsi simgesinde sizin için takibe devam edecektir!

---

## 🛠️ Kurulum ve Derleme

### C# (.NET 9 WPF) Sürümü *(Ana Sürüm)*
Hiçbir ek `.dll` gerektirmeyen tek dosyalık (Self-Contained) `.exe` üretmek için:

```powershell
cd csharp_versiyonu
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Python Sürümü *(Alternatif)*
```bash
cd python_versiyonu
pip install playwright pystray pillow plyer
python main.py
```

---

## 📂 Proje Yapısı

```text
eokul-kontenjan-takip/
├── csharp_versiyonu/          # .NET 9 WPF (Windows 11 Fluent UI) ana masaüstü uygulaması
│   ├── Assets/                # Uygulama ve sistem tepsisi (tray) ikonları
│   ├── Models/                # Okul, sınıf kontenjanı ve ayar veri modelleri
│   ├── Services/              # HTTP/Playwright sorgu motoru, Chromium indirici, ses ve başlangıç servisleri
│   ├── AlertWindows.cs        # Acil yanıp sönen alarm, EULA ve sıfırlama pencereleri
│   ├── MainWindow.xaml        # Modern vektör ikonlu WPF arayüzü
│   ├── MainWindow.xaml.cs     # Tepsi simgesi, zamanlayıcı ve kullanıcı etkileşim mantığı
│   └── UiIcons.cs             # Tüm vektör (Path) ikon tanımları
└── python_versiyonu/          # Python + Tkinter + Playwright alternatif sürüm
    └── main.py
```

---

## ⚠️ Yasal Uyarı

Bu yazılım yalnızca öğrencilerin ve velilerin halka açık e-Okul kontenjan bilgilerini kişisel olarak daha kolay takip edebilmesi amacıyla geliştirilmiştir. Milli Eğitim Bakanlığı (MEB) veya e-Okul ile hiçbir resmi bağı yoktur.
