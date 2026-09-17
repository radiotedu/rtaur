using System.Drawing;
using System.Windows.Forms;
using RadioTEDU.AutoReset.Models;
using RadioTEDU.AutoReset.Services;

namespace RadioTEDU.AutoReset.Forms;

public class MainDashboardForm : Form
{
    private readonly ConfigService _configService;
    private ResetConfig _config;
    private DateTime _nextResetTime;
    private MiniTimerForm? _miniTimerForm;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _tickTimer;
    private readonly ResourceMonitorService _resourceMonitor = new();

    private readonly Dictionary<string, bool> _appsRunningState = new(StringComparer.OrdinalIgnoreCase);
    private bool _isPerformingReset = false;

    // UI Controls
    private readonly Label _lblHeaderSummary;
    private readonly Label _lblNextTime;
    private readonly Label _lblCountdown;
    private readonly Label _lblLastAction;
    private readonly ListView _lstApps;
    private readonly ListView _lstPreview;

    private readonly List<Button> _actionButtons = new();
    private int _focusedButtonIndex = 0;

    public MainDashboardForm()
    {
        _configService = new ConfigService();

        // 1. Load config or run wizard
        if (!_configService.HasConfig())
        {
            _config = new ResetConfig();
            using var wizard = new SetupWizardForm(_config, _configService);
            if (wizard.ShowDialog() != DialogResult.OK)
            {
                Environment.Exit(0);
            }
        }

        _config = _configService.LoadConfig();

        // Validate that at least one app is configured
        if (_config.TargetApps.Count == 0 && !string.IsNullOrEmpty(_config.TargetExePath))
        {
            _config.TargetApps.Add(new WatchedApp { ExePath = _config.TargetExePath });
            _configService.SaveConfig(_config);
        }

        if (_config.TargetApps.Count == 0)
        {
            using var wizard = new SetupWizardForm(_config, _configService);
            wizard.ShowDialog();
            _config = _configService.LoadConfig();
        }

        // Auto-start apps if not active on startup
        Task.Run(async () =>
        {
            var results = await ProcessManager.EnsureAllRunningAsync(_config.TargetApps);
            foreach (var r in results)
            {
                _appsRunningState[r.AppName] = true;
            }
        });

        _nextResetTime = ScheduleEngine.CalculateNextReset(_config, DateTime.Now);

        // Window Styling (White Mode / Light Theme)
        Text = "RadioTEDU Auto Reset & Watchdog System (7/24)";
        BackColor = Color.FromArgb(248, 250, 252);
        ForeColor = Color.FromArgb(15, 23, 42);
        ClientSize = new Size(960, 680);
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        KeyPreview = true;

        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
        }
        catch { }

        // Top Header
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16, 10, 16, 10)
        };

        var lblAppTitle = new Label
        {
            Text = "📻 RadioTEDU Auto Reset & Watchdog Panel (7/24)",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            Dock = DockStyle.Left,
            AutoSize = true
        };

        var lblClock = new Label
        {
            Font = new Font("Consolas", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Right,
            AutoSize = true
        };
        pnlHeader.Controls.Add(lblAppTitle);
        pnlHeader.Controls.Add(lblClock);

        // Top Summary Card
        var pnlSummary = new Panel
        {
            Location = new Point(18, 72),
            Width = 924,
            Height = 85,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblHeaderSummary = new Label
        {
            Text = $"📱 Takip Edilen: {_config.TargetApps.Count} Uygulama | Koruma: {(_config.AutoRestartOnUnexpectedExit ? "Aktif" : "Devre Dışı")}",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(14, 10),
            AutoSize = true
        };

        _lblNextTime = new Label
        {
            Text = $"Sonraki Planlı Reset: {_nextResetTime:dd.MM.yyyy dddd HH:mm:ss} (+{_config.IntervalHours}s döngü)",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(14, 34),
            AutoSize = true
        };

        _lblCountdown = new Label
        {
            Text = "⏳ Kalan Süre: 00:00:00",
            Font = new Font("Consolas", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 163, 74),
            Location = new Point(14, 55),
            AutoSize = true
        };

        pnlSummary.Controls.Add(_lblHeaderSummary);
        pnlSummary.Controls.Add(_lblNextTime);
        pnlSummary.Controls.Add(_lblCountdown);

        // Monitored Applications Table & Controls
        var lblAppsTitle = new Label
        {
            Text = "📱 7/24 Takip Edilen Uygulamalar:",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(18, 166),
            AutoSize = true
        };

        var btnAddAppQuick = new Button
        {
            Text = "➕ Uygulama Ekle",
            Location = new Point(710, 163),
            Width = 120,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(22, 163, 74),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnAddAppQuick.FlatAppearance.BorderSize = 0;
        btnAddAppQuick.Click += (s, e) => AddNewApplicationQuick();

        var btnRemoveAppQuick = new Button
        {
            Text = "➖ Kaldır",
            Location = new Point(838, 163),
            Width = 104,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(220, 38, 38),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnRemoveAppQuick.FlatAppearance.BorderSize = 0;
        btnRemoveAppQuick.Click += (s, e) => RemoveSelectedApplication();

        _lstApps = new ListView
        {
            Location = new Point(18, 193),
            Width = 924,
            Height = 175,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Consolas", 9F),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _lstApps.Columns.Add("Durum", 130);
        _lstApps.Columns.Add("Uygulama Adı", 170);
        _lstApps.Columns.Add("PID", 80);
        _lstApps.Columns.Add("RAM (MB)", 100);
        _lstApps.Columns.Add("CPU %", 90);
        _lstApps.Columns.Add("Çalışma Süresi", 130);
        _lstApps.Columns.Add("Dosya Yolu", 220);

        // 1-Week Schedule List
        var lblScheduleTitle = new Label
        {
            Text = "📅 Planlanan 1 Haftalık Reset Takvimi:",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(18, 375),
            AutoSize = true
        };

        _lstPreview = new ListView
        {
            Location = new Point(18, 398),
            Width = 924,
            Height = 135,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Consolas", 8.5F),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        _lstPreview.Columns.Add("No", 45);
        _lstPreview.Columns.Add("Reset Tarihi", 150);
        _lstPreview.Columns.Add("Gün", 150);
        _lstPreview.Columns.Add("Planlanan Saat", 130);
        _lstPreview.Columns.Add("Döngü Süresi", 120);

        _lblLastAction = new Label
        {
            Text = "Sistem hazır. 7/24 izleme ve çoklu uygulama takibi aktif.",
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(18, 540),
            AutoSize = true,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };

        // Bottom Arrow-Key Navigable Action Bar
        var pnlButtons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 68,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12)
        };

        var btnResetAll = CreateActionButton("⚡ Hepsini Resetle", Color.FromArgb(217, 119, 6), async () => await ConfirmAndResetAllAsync());
        var btnMini = CreateActionButton("⏱️ Mini Sayaç", Color.FromArgb(13, 148, 136), () => SwitchToMiniTimer());
        var btnConfig = CreateActionButton("⚙️ Ayarlar & Koruma", Color.FromArgb(37, 99, 235), () => OpenWizard());
        var btnHistory = CreateActionButton("📋 Geçmiş", Color.FromArgb(124, 58, 237), () => ShowHistoryDialog());
        var btnFactoryReset = CreateActionButton("🧹 RTAUR Sıfırla", Color.FromArgb(180, 83, 9), () => ConfirmFactoryReset());
        var btnExit = CreateActionButton("❌ Çıkış", Color.FromArgb(220, 38, 38), () => ConfirmExit());

        _actionButtons.AddRange(new[] { btnResetAll, btnMini, btnConfig, btnHistory, btnFactoryReset, btnExit });

        int left = 14;
        int btnWidth = 145;
        int gap = 10;
        for (int i = 0; i < _actionButtons.Count; i++)
        {
            _actionButtons[i].Left = left + (i * (btnWidth + gap));
            _actionButtons[i].Top = 14;
            _actionButtons[i].Width = btnWidth;
            _actionButtons[i].Height = 38;
            pnlButtons.Controls.Add(_actionButtons[i]);
        }

        Controls.Add(pnlHeader);
        Controls.Add(pnlSummary);
        Controls.Add(lblAppsTitle);
        Controls.Add(btnAddAppQuick);
        Controls.Add(btnRemoveAppQuick);
        Controls.Add(_lstApps);
        Controls.Add(lblScheduleTitle);
        Controls.Add(_lstPreview);
        Controls.Add(_lblLastAction);
        Controls.Add(pnlButtons);

        // System Tray (NotifyIcon) Setup
        _notifyIcon = new NotifyIcon
        {
            Icon = Icon ?? SystemIcons.Application,
            Text = "RadioTEDU Auto Reset (7/24)",
            Visible = true
        };

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("📻 Ana Paneli Aç", null, (s, e) => RestoreMainWindow());
        trayMenu.Items.Add("⏱️ Mini Sayacı Göster", null, (s, e) => SwitchToMiniTimer());
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("⚡ Tüm Uygulamaları Resetle", null, async (s, e) => await ConfirmAndResetAllAsync());
        trayMenu.Items.Add("-");
        trayMenu.Items.Add("❌ Çıkış", null, (s, e) => ConfirmExit(force: true));
        _notifyIcon.ContextMenuStrip = trayMenu;
        _notifyIcon.DoubleClick += (s, e) => RestoreMainWindow();

        // Wire up Resource Monitor events
        _resourceMonitor.OnBreachThresholdExceeded += async (s, e) => await HandleResourceBreachAsync(e);

        // 1-Second Timer
        _tickTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _tickTimer.Tick += async (s, e) =>
        {
            lblClock.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

            // 1. Sample CPU and RAM across all apps
            _resourceMonitor.SampleAndCheck(_config.TargetApps, _config);

            // 2. Check Unexpected Exit ("Otomatik Aç") for each monitored app
            if (_config.AutoRestartOnUnexpectedExit && !_isPerformingReset)
            {
                foreach (var app in _config.TargetApps.Where(a => a.Enabled))
                {
                    var status = ProcessManager.GetStatus(app.ExePath);
                    _appsRunningState.TryGetValue(app.ExePath, out bool wasRunning);

                    if (wasRunning && !status.IsRunning)
                    {
                        _appsRunningState[app.ExePath] = false;
                        _ = HandleUnexpectedAppCrashAsync(app);
                    }
                    else
                    {
                        _appsRunningState[app.ExePath] = status.IsRunning;
                    }
                }
            }

            // 3. Update UI
            UpdateDashboardDisplay();

            // 4. Scheduled Reset Check
            if (DateTime.Now >= _nextResetTime)
            {
                await PerformScheduledResetAsync();
            }
        };
        _tickTimer.Start();

        PopulatePreviewList();
        UpdateDashboardDisplay();
        HighlightFocusedButton();
    }

    private Button CreateActionButton(string text, Color backColor, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = true
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

        btn.Click += (s, e) => onClick();
        btn.Enter += (s, e) =>
        {
            _focusedButtonIndex = _actionButtons.IndexOf(btn);
            HighlightFocusedButton();
        };

        return btn;
    }

    private void HighlightFocusedButton()
    {
        for (int i = 0; i < _actionButtons.Count; i++)
        {
            if (i == _focusedButtonIndex)
            {
                _actionButtons[i].FlatAppearance.BorderColor = Color.FromArgb(15, 23, 42);
                _actionButtons[i].FlatAppearance.BorderSize = 2;
            }
            else
            {
                _actionButtons[i].FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
                _actionButtons[i].FlatAppearance.BorderSize = 1;
            }
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Left)
        {
            _focusedButtonIndex = (_focusedButtonIndex - 1 + _actionButtons.Count) % _actionButtons.Count;
            _actionButtons[_focusedButtonIndex].Focus();
            HighlightFocusedButton();
            return true;
        }
        else if (keyData == Keys.Right)
        {
            _focusedButtonIndex = (_focusedButtonIndex + 1) % _actionButtons.Count;
            _actionButtons[_focusedButtonIndex].Focus();
            HighlightFocusedButton();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void UpdateDashboardDisplay()
    {
        // 1. Update Apps List
        _lstApps.BeginUpdate();
        _lstApps.Items.Clear();

        int runningCount = 0;
        foreach (var app in _config.TargetApps)
        {
            var status = ProcessManager.GetStatus(app.ExePath);
            if (status.IsRunning) runningCount++;

            _resourceMonitor.LatestMetrics.TryGetValue(app.ExePath, out var metric);

            var item = new ListViewItem(status.IsRunning ? "● ÇALIŞIYOR" : "■ KAPALI");
            item.ForeColor = status.IsRunning ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38);

            item.SubItems.Add(app.Name);
            item.SubItems.Add(status.IsRunning ? status.Pid.ToString() : "—");
            item.SubItems.Add(status.IsRunning ? $"{status.MemoryMb:F0} MB" : "—");
            item.SubItems.Add(status.IsRunning && metric != null ? $"%{metric.CpuPercent:F1}" : "—");
            item.SubItems.Add(status.IsRunning ? $"{status.Uptime.Hours:D2}s {status.Uptime.Minutes:D2}d" : "—");
            item.SubItems.Add(app.ExePath);
            item.Tag = app;

            _lstApps.Items.Add(item);
        }
        _lstApps.EndUpdate();

        // 2. Summary
        _lblHeaderSummary.Text = $"📱 Takip Edilen: {_config.TargetApps.Count} Uygulama ({runningCount}/{_config.TargetApps.Count} Aktif) | Otomatik Aç: {(_config.AutoRestartOnUnexpectedExit ? "Aktif" : "Kapalı")}";

        // 3. Countdown
        var remaining = _nextResetTime - DateTime.Now;
        if (remaining.TotalSeconds <= 0)
        {
            _lblCountdown.Text = "⏳ Kalan Süre: 00:00:00 (Yeniden başlatılıyor...)";
            _lblCountdown.ForeColor = Color.FromArgb(220, 38, 38);
        }
        else
        {
            _lblCountdown.Text = $"⏳ Kalan Süre: {ScheduleEngine.FormatRemainingTime(remaining)}";
            if (remaining.TotalMinutes < 30)
                _lblCountdown.ForeColor = Color.FromArgb(220, 38, 38);
            else if (remaining.TotalHours < 2)
                _lblCountdown.ForeColor = Color.FromArgb(217, 119, 6);
            else
                _lblCountdown.ForeColor = Color.FromArgb(22, 163, 74);
        }
    }

    private void AddNewApplicationQuick()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Yürütülebilir Dosyalar (*.exe)|*.exe|Tüm Dosyalar (*.*)|*.*",
            Title = "Takip Edilecek Yeni Uygulama Seçin"
        };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            var path = ofd.FileName;
            if (_config.TargetApps.Any(a => a.ExePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Bu uygulama zaten takip listesinde bulunuyor!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var newApp = new WatchedApp { ExePath = path };
            _config.TargetApps.Add(newApp);
            _configService.SaveConfig(_config);

            Task.Run(async () =>
            {
                await ProcessManager.EnsureRunningAsync(newApp.ExePath);
            });

            UpdateDashboardDisplay();
            _lblLastAction.Text = $"✓ '{newApp.Name}' takip listesine eklendi ve izlenmeye başlandı.";
            _lblLastAction.ForeColor = Color.FromArgb(22, 163, 74);
        }
    }

    private void RemoveSelectedApplication()
    {
        if (_lstApps.SelectedItems.Count == 0)
        {
            MessageBox.Show("Lütfen listeden kaldırmak istediğiniz uygulamayı seçin.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var selectedApp = _lstApps.SelectedItems[0].Tag as WatchedApp;
        if (selectedApp != null)
        {
            if (MessageBox.Show($"'{selectedApp.Name}' uygulamasını takip listesinden çıkarmak istiyor musunuz?", "Uygulamayı Kaldır", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _config.TargetApps.Remove(selectedApp);
                _configService.SaveConfig(_config);
                UpdateDashboardDisplay();

                _lblLastAction.Text = $"✓ '{selectedApp.Name}' takip listesinden kaldırıldı.";
                _lblLastAction.ForeColor = Color.FromArgb(217, 119, 6);
            }
        }
    }

    private async Task HandleUnexpectedAppCrashAsync(WatchedApp app)
    {
        _lblLastAction.Text = $"🚨 '{app.Name}' uygulamasının kapandığı algılandı! RTAUR tarafından geri açılıyor...";
        _lblLastAction.ForeColor = Color.FromArgb(217, 119, 6);

        var (started, msg, newPid) = await ProcessManager.EnsureRunningAsync(app.ExePath);

        _configService.AppendHistory(new ResetLogEntry
        {
            Timestamp = DateTime.Now,
            TriggerType = "UnexpectedExitWatchdog",
            Success = started,
            Message = $"'{app.Name}' kapandığı tespit edildi ve RTAUR tarafından otomatik geri açıldı (Yeni PID: {newPid})",
            PreviousPid = 0,
            NewPid = newPid
        });

        _appsRunningState[app.ExePath] = started;

        // Show popup
        AlertPopupForm.ShowPopup(app.Name, msg);
        _notifyIcon.ShowBalloonTip(4000, "RTAUR Koruma Devrede", $"'{app.Name}' kapandığı için RTAUR tarafından otomatik geri açıldı!", ToolTipIcon.Warning);

        _lblLastAction.Text = started
            ? $"✓ '{app.Name}' RTAUR tarafından geri açıldı (PID: {newPid})"
            : $"✗ '{app.Name}' geri açma başarısız: {msg}";
        _lblLastAction.ForeColor = started ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38);
    }

    private async Task HandleResourceBreachAsync(ResourceBreachEventArgs e)
    {
        if (_isPerformingReset) return;
        _isPerformingReset = true;
        try
        {
            _lblLastAction.Text = $"⚠️ {e.Reason} Sıfırlanıyor...";
            _lblLastAction.ForeColor = Color.FromArgb(220, 38, 38);

            var result = await ProcessManager.RestartTargetAsync(e.ExePath, _config.GracefulWaitSeconds);

            _configService.AppendHistory(new ResetLogEntry
            {
                Timestamp = DateTime.Now,
                TriggerType = e.TriggerType,
                Success = result.Success,
                Message = e.Reason,
                PreviousPid = result.OldPid,
                NewPid = result.NewPid
            });

            _resourceMonitor.ResetBreachTimers();
            _notifyIcon.ShowBalloonTip(5000, "RTAUR Eşik Sıfırlaması", e.Reason, ToolTipIcon.Warning);

            _lblLastAction.Text = result.Success
                ? $"✓ '{e.AppName}' kaynak koruması reset tamamlandı: {result.Message}"
                : $"✗ '{e.AppName}' kaynak koruması reset başarısız: {result.Message}";
            _lblLastAction.ForeColor = result.Success ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38);
        }
        finally
        {
            _isPerformingReset = false;
        }
    }

    private void PopulatePreviewList()
    {
        _lstPreview.Items.Clear();
        var preview = ScheduleEngine.GeneratePreviewSchedule(_config, DateTime.Now, 7);
        var trCulture = new System.Globalization.CultureInfo("tr-TR");

        int index = 1;
        foreach (var dt in preview)
        {
            var item = new ListViewItem(index.ToString());
            item.SubItems.Add(dt.ToString("dd.MM.yyyy"));
            item.SubItems.Add(dt.ToString("dddd", trCulture));
            item.SubItems.Add(dt.ToString("HH:mm:ss"));
            item.SubItems.Add($"+{_config.IntervalHours} saat");
            _lstPreview.Items.Add(item);
            index++;
        }
    }

    private async Task ConfirmAndResetAllAsync()
    {
        if (MessageBox.Show("Takip edilen TÜM uygulamaları HEMEN şimdi kapatıp yeniden başlatmak istiyor musunuz?\n(Not: Gece 03:00 zamanlaması bozulmayacaktır.)", "Tüm Uygulamaları Resetle", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _isPerformingReset = true;
            try
            {
                _lblLastAction.Text = "Tüm uygulamalar resetleniyor...";
                var results = await ProcessManager.RestartAllAppsAsync(_config.TargetApps, _config.GracefulWaitSeconds);

                foreach (var r in results)
                {
                    _configService.AppendHistory(new ResetLogEntry
                    {
                        Timestamp = DateTime.Now,
                        TriggerType = "ManualAll",
                        Success = r.Success,
                        Message = $"'{r.AppName}' manuel resetlendi: {r.Message}",
                        PreviousPid = r.OldPid,
                        NewPid = r.NewPid
                    });
                }

                _resourceMonitor.ResetBreachTimers();

                _lblLastAction.Text = $"✓ {results.Count} uygulamanın tamamı başarıyla yeniden başlatıldı.";
                _lblLastAction.ForeColor = Color.FromArgb(22, 163, 74);
            }
            finally
            {
                _isPerformingReset = false;
            }
        }
    }

    private async Task PerformScheduledResetAsync()
    {
        _isPerformingReset = true;
        try
        {
            _lblLastAction.Text = "⏰ Planlanan otomatik reset vakti geldi! Tüm uygulamalar yeniden başlatılıyor...";
            var results = await ProcessManager.RestartAllAppsAsync(_config.TargetApps, _config.GracefulWaitSeconds);

            _config.LastResetTime = DateTime.Now;
            _configService.SaveConfig(_config);

            foreach (var r in results)
            {
                _configService.AppendHistory(new ResetLogEntry
                {
                    Timestamp = DateTime.Now,
                    TriggerType = "ScheduledAll",
                    Success = r.Success,
                    Message = $"'{r.AppName}' planlı otomatik resetlendi: {r.Message}",
                    PreviousPid = r.OldPid,
                    NewPid = r.NewPid
                });
            }

            _nextResetTime = ScheduleEngine.CalculateNextReset(_config, DateTime.Now.AddSeconds(10));
            _lblNextTime.Text = $"Sonraki Planlı Reset: {_nextResetTime:dd.MM.yyyy dddd HH:mm:ss} (+{_config.IntervalHours}s döngü)";
            PopulatePreviewList();

            _resourceMonitor.ResetBreachTimers();

            _notifyIcon.ShowBalloonTip(4000, "RadioTEDU Auto Reset", $"{results.Count} uygulamanın tamamı başarıyla otomatik resetlendi!", ToolTipIcon.Info);

            _lblLastAction.Text = $"✓ {results.Count} uygulama otomatik resetlendi. (Sonraki: {_nextResetTime:dd.MM HH:mm})";
            _lblLastAction.ForeColor = Color.FromArgb(22, 163, 74);
        }
        finally
        {
            _isPerformingReset = false;
        }
    }

    private void ConfirmFactoryReset()
    {
        var confirm = MessageBox.Show(
            "RTAUR uygulamasını fabrika ayarlarına sıfırlamak ve temiz kurulum yapmak istiyor musunuz?\n\n" +
            "Bu işlem config.json ve tüm geçmiş kayıtlarını silecek, ayarları ilk defa kurulmuş gibi baştan yapılandırmanızı sağlayacaktır.",
            "RTAUR Fabrika Sıfırlama",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            _configService.FactoryReset();
            _config = new ResetConfig();

            using var wizard = new SetupWizardForm(_config, _configService);
            if (wizard.ShowDialog() == DialogResult.OK)
            {
                _config = _configService.LoadConfig();
                _nextResetTime = ScheduleEngine.CalculateNextReset(_config, DateTime.Now);
                _resourceMonitor.ResetBreachTimers();

                _lblNextTime.Text = $"Sonraki Planlı Reset: {_nextResetTime:dd.MM.yyyy dddd HH:mm:ss} (+{_config.IntervalHours}s döngü)";

                PopulatePreviewList();
                UpdateDashboardDisplay();

                _lblLastAction.Text = "✓ RTAUR başarıyla sıfırlandı ve temiz kurulum tamamlandı.";
                _lblLastAction.ForeColor = Color.FromArgb(22, 163, 74);
            }
        }
    }

    public void SwitchToMiniTimer()
    {
        Hide();

        if (_miniTimerForm == null || _miniTimerForm.IsDisposed)
        {
            _miniTimerForm = new MiniTimerForm(
                _config,
                _configService,
                onRestoreMain: RestoreMainWindow,
                getNextResetTime: () => _nextResetTime,
                onPerformReset: ConfirmAndResetAllAsync
            );
        }

        _miniTimerForm.Show();
        _miniTimerForm.BringToFront();
    }

    public void RestoreMainWindow()
    {
        if (_miniTimerForm != null && !_miniTimerForm.IsDisposed)
        {
            _miniTimerForm.Hide();
        }

        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
    }

    private void OpenWizard()
    {
        using var wizard = new SetupWizardForm(_config, _configService);
        if (wizard.ShowDialog() == DialogResult.OK)
        {
            _config = _configService.LoadConfig();
            _nextResetTime = ScheduleEngine.CalculateNextReset(_config, DateTime.Now);

            _lblNextTime.Text = $"Sonraki Planlı Reset: {_nextResetTime:dd.MM.yyyy dddd HH:mm:ss} (+{_config.IntervalHours}s döngü)";

            PopulatePreviewList();
            UpdateDashboardDisplay();

            _lblLastAction.Text = "Ayarlar başarıyla güncellendi.";
            _lblLastAction.ForeColor = Color.FromArgb(22, 163, 74);
        }
    }

    private void ShowHistoryDialog()
    {
        var logs = _configService.LoadHistory();

        using var historyForm = new Form
        {
            Text = "📋 RTAUR Reset Geçmişi ve Günlüğü",
            ClientSize = new Size(740, 450),
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(15, 23, 42),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lv = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Consolas", 9F)
        };
        lv.Columns.Add("Tarih & Saat", 145);
        lv.Columns.Add("Tetikleyici", 140);
        lv.Columns.Add("Durum", 80);
        lv.Columns.Add("PID Değişimi", 110);
        lv.Columns.Add("Açıklama / Sebep", 240);

        foreach (var log in logs)
        {
            var item = new ListViewItem(log.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"));
            item.SubItems.Add(log.TriggerType);
            item.SubItems.Add(log.Success ? "✓ Başarılı" : "✗ Hata");
            item.SubItems.Add($"{log.PreviousPid} -> {log.NewPid}");
            item.SubItems.Add(log.Message);
            lv.Items.Add(item);
        }

        historyForm.Controls.Add(lv);
        historyForm.ShowDialog(this);
    }

    private void ConfirmExit(bool force = false)
    {
        if (force || MessageBox.Show("RadioTEDU Auto Reset uygulamasından tamamen çıkmak istiyor musunuz?\n(Çıkarsanız otomatik reset duracaktır!)", "Çıkışı Onayla", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Environment.Exit(0);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _notifyIcon.ShowBalloonTip(2500, "RadioTEDU Auto Reset", "Uygulama arka planda sistem tepsisinde çalışmaya devam ediyor.\nMini sayaç ekranı açıldı.", ToolTipIcon.Info);
            SwitchToMiniTimer();
        }
        base.OnFormClosing(e);
    }
}
