using System.Drawing;
using System.Windows.Forms;
using RadioTEDU.AutoReset.Models;
using RadioTEDU.AutoReset.Services;

namespace RadioTEDU.AutoReset.Forms;

public class SetupWizardForm : Form
{
    private readonly ResetConfig _config;
    private readonly ConfigService _configService;

    // Multi-App List
    private readonly TextBox _txtExeInput;
    private readonly Button _btnBrowse;
    private readonly Button _btnAddApp;
    private readonly Button _btnRemoveApp;
    private readonly ListView _lstApps;

    // Tab 1: Schedule
    private readonly ComboBox _cboInterval;
    private readonly ComboBox _cboHour;
    private readonly ListView _lstPreview;
    private readonly CheckBox _chkAutoStartNow;
    private readonly Label _lblAutoStartStatus;

    // Tab 2: Advanced Watchdogs
    private readonly CheckBox _chkAutoRestartOnUnexpectedExit;
    private readonly CheckBox _chkCpuWatchdog;
    private readonly ComboBox _cboCpuThreshold;
    private readonly ComboBox _cboCpuMinutes;
    private readonly CheckBox _chkRamWatchdog;
    private readonly ComboBox _cboRamThreshold;
    private readonly ComboBox _cboRamMinutes;

    private readonly Button _btnSave;
    private readonly Button _btnCancel;

    public bool ConfigurationSaved { get; private set; }

    public SetupWizardForm(ResetConfig config, ConfigService configService)
    {
        _config = config;
        _configService = configService;

        Text = "RadioTEDU Auto Reset — Kurulum ve Çoklu Uygulama Yapılandırması";
        BackColor = Color.FromArgb(248, 250, 252);
        ForeColor = Color.FromArgb(15, 23, 42);
        ClientSize = new Size(680, 640);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblTitle = new Label
        {
            Text = "⚙️ RTAUR Çoklu Uygulama ve Koruma Yapılandırması",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            Location = new Point(16, 8),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "7/24 takip edilecek uygulamaları listeye ekleyin, otomatik çökme korumasını ve reset döngüsünü ayarlayın.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(18, 32),
            AutoSize = true
        };

        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSubtitle);

        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        // ==========================================
        // TAB 1: UYGULAMALAR & ZAMANLAMA
        // ==========================================
        var tabGeneral = new TabPage("📱 Takip Edilecek Uygulamalar & Zamanlama")
        {
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(12)
        };

        var lblAppHeader = new Label
        {
            Text = "1. Takip Edilecek Uygulamalar (.EXE):",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(12, 8),
            AutoSize = true,
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        // Starts 100% EMPTY, no default suggestions!
        _txtExeInput = new TextBox
        {
            Location = new Point(12, 28),
            Width = 430,
            Font = new Font("Segoe UI", 9F),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Eklenecek .exe dosya yolunu girin veya Gözat'a tıklayın..."
        };

        _btnBrowse = new Button
        {
            Text = "Gözat...",
            Location = new Point(448, 27),
            Width = 85,
            Height = 25,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnBrowse.FlatAppearance.BorderSize = 0;
        _btnBrowse.Click += (s, e) =>
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Yürütülebilir Dosyalar (*.exe)|*.exe|Tüm Dosyalar (*.*)|*.*",
                Title = "Takip Edilecek Uygulamayı Seçin"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _txtExeInput.Text = ofd.FileName;
            }
        };

        _btnAddApp = new Button
        {
            Text = "➕ Ekle",
            Location = new Point(540, 27),
            Width = 85,
            Height = 25,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(22, 163, 74), // Green
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnAddApp.FlatAppearance.BorderSize = 0;
        _btnAddApp.Click += (s, e) => AddAppToList();

        // Apps List
        _lstApps = new ListView
        {
            Location = new Point(12, 58),
            Width = 530,
            Height = 90,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 8.5F)
        };
        _lstApps.Columns.Add("Uygulama Adı", 140);
        _lstApps.Columns.Add("Dosya Yolu", 360);

        // Populate already configured apps if any
        foreach (var app in _config.TargetApps)
        {
            var item = new ListViewItem(app.Name);
            item.SubItems.Add(app.ExePath);
            item.Tag = app;
            _lstApps.Items.Add(item);
        }

        _btnRemoveApp = new Button
        {
            Text = "➖ Kaldır",
            Location = new Point(548, 58),
            Width = 77,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(220, 38, 38), // Red
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnRemoveApp.FlatAppearance.BorderSize = 0;
        _btnRemoveApp.Click += (s, e) => RemoveSelectedApp();

        var lblInterval = new Label
        {
            Text = "2. Reset Döngüsü:",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(12, 155),
            AutoSize = true,
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        _cboInterval = new ComboBox
        {
            Location = new Point(12, 175),
            Width = 285,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9F)
        };
        _cboInterval.Items.AddRange(new object[] { "24 Saat (Her Gün)", "48 Saat (İki Günde Bir)" });
        _cboInterval.SelectedIndex = _config.IntervalHours == 48 ? 1 : 0;
        _cboInterval.SelectedIndexChanged += (s, e) => RefreshPreview();

        var lblHour = new Label
        {
            Text = "3. Reset Saati:",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(315, 155),
            AutoSize = true,
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        _cboHour = new ComboBox
        {
            Location = new Point(315, 175),
            Width = 310,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9F)
        };
        for (int h = 0; h < 24; h++)
        {
            string label = $"{h:D2}:00";
            if (h == 3) label += " (Önerilen - Gece 3)";
            else if (h == 0) label += " (Gece Yarısı)";
            else if (h == 5) label += " (Sabah 5)";
            _cboHour.Items.Add(label);
        }
        _cboHour.SelectedIndex = Math.Clamp(_config.TargetHour, 0, 23);
        _cboHour.SelectedIndexChanged += (s, e) => RefreshPreview();

        _chkAutoStartNow = new CheckBox
        {
            Text = "Kapalı olan uygulamaları kurulum tamamlandığında hemen başlat",
            Location = new Point(12, 208),
            Width = 610,
            Checked = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 163, 74)
        };

        _lblAutoStartStatus = new Label
        {
            Location = new Point(12, 230),
            Width = 610,
            Height = 18,
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = "Listeye dilediğiniz kadar uygulama (.exe) ekleyebilirsiniz."
        };

        var lblPreviewTitle = new Label
        {
            Text = "📅 1 Haftalık Önizleme Takvimi:",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(12, 252),
            AutoSize = true,
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        _lstPreview = new ListView
        {
            Location = new Point(12, 274),
            Width = 620,
            Height = 220,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Consolas", 8.5F)
        };
        _lstPreview.Columns.Add("No", 40);
        _lstPreview.Columns.Add("Reset Tarihi", 140);
        _lstPreview.Columns.Add("Gün", 140);
        _lstPreview.Columns.Add("Hedef Saat", 130);
        _lstPreview.Columns.Add("Döngü", 120);

        tabGeneral.Controls.Add(lblAppHeader);
        tabGeneral.Controls.Add(_txtExeInput);
        tabGeneral.Controls.Add(_btnBrowse);
        tabGeneral.Controls.Add(_btnAddApp);
        tabGeneral.Controls.Add(_lstApps);
        tabGeneral.Controls.Add(_btnRemoveApp);
        tabGeneral.Controls.Add(lblInterval);
        tabGeneral.Controls.Add(_cboInterval);
        tabGeneral.Controls.Add(lblHour);
        tabGeneral.Controls.Add(_cboHour);
        tabGeneral.Controls.Add(_chkAutoStartNow);
        tabGeneral.Controls.Add(_lblAutoStartStatus);
        tabGeneral.Controls.Add(lblPreviewTitle);
        tabGeneral.Controls.Add(_lstPreview);

        // ==========================================
        // TAB 2: GELİŞMİŞ KORUMA & KAYNAK TAKİBİ
        // ==========================================
        var tabAdvanced = new TabPage("🛡️ Gelişmiş Koruma & Kaynak Takibi")
        {
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(16)
        };

        // 1. Kapanma Koruması
        var grpCrash = new GroupBox
        {
            Text = "🚨 Kapanma Koruması (\"Otomatik Aç\")",
            Location = new Point(12, 12),
            Width = 620,
            Height = 110,
            ForeColor = Color.FromArgb(217, 119, 6),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.White
        };

        _chkAutoRestartOnUnexpectedExit = new CheckBox
        {
            Text = "Otomatik Aç: Takip edilen herhangi bir uygulama kapanırsa anında geri aç",
            Location = new Point(16, 26),
            Width = 580,
            Checked = _config.AutoRestartOnUnexpectedExit,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 163, 74)
        };

        var lblCrashHelp = new Label
        {
            Text = "Listeye eklenen uygulamalardan herhangi biri çökerse veya zorla kapatılırsa, RTAUR yalnızca o uygulamayı hemen geri açar ve ekranda 'RTAUR Tarafından Geri Açıldı!' popup bildirimi gösterir.",
            Location = new Point(16, 52),
            Width = 580,
            Height = 45,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(100, 116, 139)
        };

        grpCrash.Controls.Add(_chkAutoRestartOnUnexpectedExit);
        grpCrash.Controls.Add(lblCrashHelp);

        // 2. CPU Watchdog
        var grpCpu = new GroupBox
        {
            Text = "⚡ CPU Yük Takipli Otomatik Resetleme (Native: Kapalı)",
            Location = new Point(12, 135),
            Width = 620,
            Height = 150,
            ForeColor = Color.FromArgb(37, 99, 235),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.White
        };

        _chkCpuWatchdog = new CheckBox
        {
            Text = "Aşırı CPU Tüketiminde Otomatik Resetlemeyi Aktif Et",
            Location = new Point(16, 26),
            Width = 580,
            Checked = _config.CpuWatchdogEnabled,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42)
        };

        var lblCpuThreshold = new Label
        {
            Text = "Tetikleme Eşiği (CPU):",
            Location = new Point(16, 56),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        _cboCpuThreshold = new ComboBox
        {
            Location = new Point(16, 76),
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9F)
        };
        _cboCpuThreshold.Items.AddRange(new object[] { "%80 ve üzeri", "%85 ve üzeri", "%90 ve üzeri", "%95 ve üzeri (Önerilen)", "%98 ve üzeri", "%100 (Tam Yük)" });
        _cboCpuThreshold.SelectedIndex = 3;

        var lblCpuMinutes = new Label
        {
            Text = "Ne Kadar Süre Devam Ederse?:",
            Location = new Point(300, 56),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        _cboCpuMinutes = new ComboBox
        {
            Location = new Point(300, 76),
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9F)
        };
        _cboCpuMinutes.Items.AddRange(new object[] { "5 Dakika boyunca", "10 Dakika boyunca", "15 Dakika boyunca (Önerilen)", "30 Dakika boyunca", "60 Dakika boyunca" });
        _cboCpuMinutes.SelectedIndex = 2;

        var lblCpuHelp = new Label
        {
            Text = "Takip edilen bir uygulamanın CPU kullanımı seçilen süre boyunca bu eşiğin üzerinde kalırsa sistem kilitlenmesini ve 502 hatalarını önlemek için otomatik resetlenir.",
            Location = new Point(16, 110),
            Width = 580,
            Height = 32,
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139)
        };

        grpCpu.Controls.Add(_chkCpuWatchdog);
        grpCpu.Controls.Add(lblCpuThreshold);
        grpCpu.Controls.Add(_cboCpuThreshold);
        grpCpu.Controls.Add(lblCpuMinutes);
        grpCpu.Controls.Add(_cboCpuMinutes);
        grpCpu.Controls.Add(lblCpuHelp);

        // 3. RAM Watchdog (Up to 16 GB)
        var grpRam = new GroupBox
        {
            Text = "🧠 Bellek (RAM) Şişme Takipli Otomatik Resetleme (Native: Kapalı)",
            Location = new Point(12, 298),
            Width = 620,
            Height = 150,
            ForeColor = Color.FromArgb(124, 58, 237),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.White
        };

        _chkRamWatchdog = new CheckBox
        {
            Text = "Aşırı Bellek (RAM) Şişmesinde Otomatik Resetlemeyi Aktif Et",
            Location = new Point(16, 26),
            Width = 580,
            Checked = _config.RamWatchdogEnabled,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42)
        };

        var lblRamThreshold = new Label
        {
            Text = "Tetikleme Eşiği (RAM - 16 GB'a Kadar):",
            Location = new Point(16, 56),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        _cboRamThreshold = new ComboBox
        {
            Location = new Point(16, 76),
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9F)
        };
        _cboRamThreshold.Items.AddRange(new object[]
        {
            "500 MB",
            "1000 MB (1 GB)",
            "1024 MB (Önerilen)",
            "2048 MB (2 GB)",
            "3072 MB (3 GB)",
            "4096 MB (4 GB)",
            "6144 MB (6 GB)",
            "8192 MB (8 GB)",
            "12288 MB (12 GB)",
            "16384 MB (16 GB - Maksimum)"
        });

        _cboRamThreshold.SelectedIndex = _config.RamThresholdMb switch
        {
            <= 500 => 0,
            <= 1000 => 1,
            <= 1024 => 2,
            <= 2048 => 3,
            <= 3072 => 4,
            <= 4096 => 5,
            <= 6144 => 6,
            <= 8192 => 7,
            <= 12288 => 8,
            _ => 9
        };

        var lblRamMinutes = new Label
        {
            Text = "Ne Kadar Süre Devam Ederse?:",
            Location = new Point(300, 56),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        _cboRamMinutes = new ComboBox
        {
            Location = new Point(300, 76),
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9F)
        };
        _cboRamMinutes.Items.AddRange(new object[] { "5 Dakika boyunca", "10 Dakika boyunca", "15 Dakika boyunca (Önerilen)", "30 Dakika boyunca", "60 Dakika boyunca" });
        _cboRamMinutes.SelectedIndex = 2;

        var lblRamHelp = new Label
        {
            Text = "Takip edilen herhangi bir uygulamanın RAM'i seçilen eşiği aşar ve süre boyunca inmezse güvenli şekilde yeniden başlatılır.",
            Location = new Point(16, 110),
            Width = 580,
            Height = 32,
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139)
        };

        grpRam.Controls.Add(_chkRamWatchdog);
        grpRam.Controls.Add(lblRamThreshold);
        grpRam.Controls.Add(_cboRamThreshold);
        grpRam.Controls.Add(lblRamMinutes);
        grpRam.Controls.Add(_cboRamMinutes);
        grpRam.Controls.Add(lblRamHelp);

        tabAdvanced.Controls.Add(grpCrash);
        tabAdvanced.Controls.Add(grpCpu);
        tabAdvanced.Controls.Add(grpRam);

        tabControl.TabPages.Add(tabGeneral);
        tabControl.TabPages.Add(tabAdvanced);

        // Bottom Actions
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        _btnSave = new Button
        {
            Text = "✓ Ayarları Kaydet ve Başlat",
            Location = new Point(440, 10),
            Width = 215,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += async (s, e) => await SaveAndFinishAsync();

        _btnCancel = new Button
        {
            Text = "İptal",
            Location = new Point(345, 10),
            Width = 85,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnCancel.FlatAppearance.BorderSize = 0;
        _btnCancel.Click += (s, e) => Close();

        pnlBottom.Controls.Add(_btnSave);
        pnlBottom.Controls.Add(_btnCancel);

        Controls.Add(tabControl);
        Controls.Add(pnlHeader);
        Controls.Add(pnlBottom);

        RefreshPreview();
    }

    private void AddAppToList()
    {
        var path = _txtExeInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show("Lütfen bir .exe dosya yolu seçin veya girin!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Belirtilen konumda geçerli bir .exe dosyası bulunamadı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Check if already in list
        foreach (ListViewItem item in _lstApps.Items)
        {
            if (item.SubItems[1].Text.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Bu uygulama zaten listede bulunuyor!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        var watchedApp = new WatchedApp { ExePath = path };
        var lvi = new ListViewItem(watchedApp.Name);
        lvi.SubItems.Add(path);
        lvi.Tag = watchedApp;
        _lstApps.Items.Add(lvi);

        _txtExeInput.Clear();
    }

    private void RemoveSelectedApp()
    {
        if (_lstApps.SelectedItems.Count > 0)
        {
            _lstApps.Items.Remove(_lstApps.SelectedItems[0]);
        }
        else
        {
            MessageBox.Show("Lütfen kaldırmak istediğiniz uygulamayı listeden seçin.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void RefreshPreview()
    {
        _lstPreview.Items.Clear();

        var tempConfig = new ResetConfig
        {
            IntervalHours = _cboInterval.SelectedIndex == 1 ? 48 : 24,
            TargetHour = _cboHour.SelectedIndex >= 0 ? _cboHour.SelectedIndex : 3,
            TargetMinute = 0
        };

        var preview = ScheduleEngine.GeneratePreviewSchedule(tempConfig, DateTime.Now, 7);
        var trCulture = new System.Globalization.CultureInfo("tr-TR");

        int index = 1;
        foreach (var dt in preview)
        {
            var item = new ListViewItem(index.ToString());
            item.SubItems.Add(dt.ToString("dd.MM.yyyy"));
            item.SubItems.Add(dt.ToString("dddd", trCulture));
            item.SubItems.Add(dt.ToString("HH:mm:ss"));
            item.SubItems.Add($"+{tempConfig.IntervalHours} saat");
            _lstPreview.Items.Add(item);
            index++;
        }
    }

    private async Task SaveAndFinishAsync()
    {
        if (_lstApps.Items.Count == 0)
        {
            MessageBox.Show("Lütfen takip edilecek en az bir uygulama (.exe) ekleyin!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var appList = new List<WatchedApp>();
        foreach (ListViewItem item in _lstApps.Items)
        {
            if (item.Tag is WatchedApp wa)
            {
                appList.Add(wa);
            }
            else
            {
                appList.Add(new WatchedApp { ExePath = item.SubItems[1].Text });
            }
        }

        _config.TargetApps = appList;
        _config.IntervalHours = _cboInterval.SelectedIndex == 1 ? 48 : 24;
        _config.TargetHour = _cboHour.SelectedIndex;
        _config.TargetMinute = 0;

        _config.AutoRestartOnUnexpectedExit = _chkAutoRestartOnUnexpectedExit.Checked;

        _config.CpuWatchdogEnabled = _chkCpuWatchdog.Checked;
        _config.CpuThresholdPercent = _cboCpuThreshold.SelectedIndex switch
        {
            0 => 80.0,
            1 => 85.0,
            2 => 90.0,
            3 => 95.0,
            4 => 98.0,
            _ => 100.0
        };
        _config.CpuSustainedMinutes = _cboCpuMinutes.SelectedIndex switch
        {
            0 => 5,
            1 => 10,
            2 => 15,
            3 => 30,
            _ => 60
        };

        _config.RamWatchdogEnabled = _chkRamWatchdog.Checked;
        _config.RamThresholdMb = _cboRamThreshold.SelectedIndex switch
        {
            0 => 500.0,
            1 => 1000.0,
            2 => 1024.0,
            3 => 2048.0,
            4 => 3072.0,
            5 => 4096.0,
            6 => 6144.0,
            7 => 8192.0,
            8 => 12288.0,
            _ => 16384.0
        };
        _config.RamSustainedMinutes = _cboRamMinutes.SelectedIndex switch
        {
            0 => 5,
            1 => 10,
            2 => 15,
            3 => 30,
            _ => 60
        };

        // Auto-start closed apps if requested
        if (_chkAutoStartNow.Checked)
        {
            _lblAutoStartStatus.Text = "Kapalı uygulamalar kontrol ediliyor ve başlatılıyor...";
            _lblAutoStartStatus.ForeColor = Color.FromArgb(217, 119, 6);

            var startResults = await ProcessManager.EnsureAllRunningAsync(_config.TargetApps);
            foreach (var r in startResults)
            {
                if (r.Started)
                {
                    _configService.AppendHistory(new ResetLogEntry
                    {
                        Timestamp = DateTime.Now,
                        TriggerType = "SetupAutoStart",
                        Success = true,
                        Message = $"Kurulumda '{r.AppName}' aktif olmadığı tespit edildi ve otomatik başlatıldı (PID: {r.Pid})",
                        PreviousPid = 0,
                        NewPid = r.Pid
                    });
                }
            }
        }

        _configService.SaveConfig(_config);
        ConfigurationSaved = true;
        DialogResult = DialogResult.OK;
        Close();
    }
}
