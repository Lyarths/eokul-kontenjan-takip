# -*- coding: utf-8 -*-
"""
e-Okul Boş Kontenjan Takip Uygulaması (v2.0 — Doğrudan HTTP & PyQt6 Modern-Klasik Arayüz)

Çalıştırma:
    python main.py

Özellikler:
    - Chromium veya tarayıcı gerektirmez; doğrudan HTTP PostBack (cURL mantığı) ile çalışır.
    - Modüler mimari:
        core/config.py       -> Sabitler, dosya yolları ve loglama
        core/eokul_client.py -> Düz HTTP e-Okul sorgulama ve oturum önbellekleme motoru
        core/storage.py      -> Atomik JSON veri ve ayar yönetimi (%APPDATA%\\E-Okul Checker)
        core/startup.py      -> Windows açılışında otomatik başlatma (shell:startup)
        ui/theme.py          -> Modern-Klasik Koyu ve Açık tema QSS şablonları
        ui/dialogs.py        -> Ayarlar, EULA, Sınıf Seçimi ve Acil Uyarı pencereleri
        ui/main_window.py    -> Ana pencere ve Sistem Tepsisi (Tray) yönetimi
"""

import ctypes
import os
import sys
import traceback

from core.config import APP_NAME, crash_log_yaz


def _windows_gorev_cubugu_kimligi_ayarla() -> None:
    """Görev çubuğunda Python ikonu yerine uygulama logosunun görünmesini sağlar."""
    if os.name == "nt":
        try:
            ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID(
                "eokul.kontenjan.takip.2.0"
            )
        except Exception:
            pass


def main() -> None:
    _windows_gorev_cubugu_kimligi_ayarla()

    from PyQt6.QtWidgets import QApplication
    from ui.main_window import MainWindow

    app = QApplication(sys.argv)
    app.setApplicationName(APP_NAME)
    app.setQuitOnLastWindowClosed(False)

    baslangicta_gizli = "--minimized" in sys.argv
    pencere = MainWindow(
        ana_betik_yolu=os.path.abspath(__file__),
        baslangicta_gizli=baslangicta_gizli,
    )

    if not baslangicta_gizli:
        pencere.show()

    sys.exit(app.exec())


if __name__ == "__main__":
    try:
        main()
    except Exception:
        hata_metni = traceback.format_exc()
        dosya = crash_log_yaz(hata_metni)
        mesaj = "Program başlatılırken beklenmeyen bir hata oluştu.\n\n"
        if dosya:
            mesaj += f"Ayrıntılar şu dosyaya kaydedildi:\n{dosya}\n\n"
        mesaj += hata_metni[-500:]
        if os.name == "nt":
            try:
                ctypes.windll.user32.MessageBoxW(
                    0, mesaj, f"{APP_NAME} - Hata", 0x10
                )
            except Exception:
                pass
        sys.exit(1)
