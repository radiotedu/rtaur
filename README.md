# 📻 RadioTEDU Auto Reset (RTAUR)

<div align="center">
  <img src="assets/logo.png" alt="RTAUR Logo" width="280" />
  <p><strong>RadioTEDU 7/24 Kesintisiz Yayın, Çoklu Uygulama İzleme ve Otomatik Reset Sistemi</strong></p>
</div>

---

## 🎯 Genel Bakış

**RTAUR (RadioTEDU Auto Reset)**, Windows 10/11 ortamında 7/24 çalışan radyo yayın yazılımları, akış sunucuları ve kritik arka plan servislerinin belleğin şişmesi, donması veya beklenmedik çöküşlerine karşı kesintisiz çalışmasını garanti altına alan hafif ve modern bir watchdog / auto-restart uygulamasıdır.

- ⏰ **Akıllı Zamanlanmış Reset:** 24 veya 48 saatlik periyotlarla belirlenen saatte (örneğin her gece 03:00) otomatik ve zarif (graceful) yeniden başlatma.
- 📋 **Çoklu Uygulama Takibi (Multi-Process Monitoring):** Birden fazla hedef .exe dosyasını eşzamanlı izleme, PID, bellek (MB) ve işlemci (%) takibi.
- 🛡️ **Otomatik Geri Açma (Crash Watchdog):** Hedef uygulamalardan biri çöker veya kullanıcı tarafından kapatılırsa saniyeler içinde otomatik olarak geri açar ve kullanıcıyı bilgilendirir.
- 📈 **Kaynak Tüketim Koruması (CPU & RAM Watchdog):** 
  - CPU %95+ aşımında veya
  - RAM belirlenen eşiği (512 MB – 16 GB) aştığında ve bu durum 15 dakika boyunca devam ettiğinde uygulamayı güvenle yeniden başlatır.
- ⏱️ **Dinamik Ölçeklenebilir Mini Sayaç:** Ana pencere kapatıldığında sistem tepsisine küçülür ve istenirse ekranda pencere boyutuna göre yazı boyutu otomatik büyüyen bir mini geri sayım sayacı açılır.
- 🎨 **White Mode (Açık Tema):** Sade, okunabilir, göz yormayan modern açık arayüz tasarımı.
- ⚡ **Fabrika Ayarlarına Sıfırlama:** Tek tıkla tüm yapılandırma ve geçmiş kayıtlarını güvenle temizleme imkânı.

---

## 🏗️ Proje Yapısı

\\\	ext
rtaur/
├── assets/
│   └── logo.png
├── src/
│   └── RadioTEDU.AutoReset/
│       ├── Forms/
│       │   ├── SetupWizardForm.cs     # İlk kurulum sihirbazı
│       │   ├── MainDashboardForm.cs   # Ana kontrol paneli & canlı tablo
│       │   └── MiniTimerForm.cs       # Ölçeklenebilir mini sayaç
│       ├── Models/
│       │   └── ResetModels.cs         # Veri yapılandırma ve süreç modelleri
│       ├── Services/
│       │   ├── ConfigService.cs       # JSON ayar & log yönetimi
│       │   ├── ProcessManager.cs      # Süreç izleme, kapatma ve açma
│       │   ├── ResourceMonitorService.cs # CPU / RAM eşik takibi
│       │   └── ScheduleEngine.cs      # Zamanlama hesaplayıcı
│       ├── ConsoleRenderer.cs         # Terminal / Spectre.Console motoru
│       ├── Program.cs
│       └── RadioTEDU.AutoReset.csproj
├── tests/
│   └── RadioTEDU.AutoReset.Tests/
│       ├── UnitTest1.cs
│       └── RadioTEDU.AutoReset.Tests.csproj
├── RadioTEDU.AutoReset.slnx
├── .gitignore
└── README.md
\\\

---

## 🛠️ Gereksinimler ve Derleme

- **.NET SDK 10.0+ (Windows Desktop Workload)**
- **Windows 10 / 11**

### Derleme

\\\ash
dotnet build RadioTEDU.AutoReset.slnx
\\\

### Testleri Çalıştırma

\\\ash
dotnet test RadioTEDU.AutoReset.slnx
\\\

### Tek Dosya (Single-File) EXE Olarak Yayınlama

\\\powershell
dotnet publish src\RadioTEDU.AutoReset\RadioTEDU.AutoReset.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
\\\

---

## 📄 Lisans

Bu proje [RadioTEDU](https://github.com/radiotedu) ekibi tarafından radyo yayınlarının sürekliliği ve otomasyonu amacıyla geliştirilmiştir.
