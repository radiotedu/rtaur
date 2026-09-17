using System.Drawing;
using System.Windows.Forms;
using RadioTEDU.AutoReset.Models;
using RadioTEDU.AutoReset.Services;

namespace RadioTEDU.AutoReset.Forms;

public class MiniTimerForm : Form
{
    private readonly ResetConfig _config;
    private readonly ConfigService _configService;
    private readonly Action _onRestoreMain;
    private readonly Func<DateTime> _getNextResetTime;
    private readonly Func<Task> _onPerformReset;

    private readonly Label _lblHeader;
    private readonly Label _lblCountdown;
    private readonly Label _lblStatus;
    private readonly Panel _pnlBottom;
    private readonly Button _btnRestore;
    private readonly Button _btnResetNow;
    private readonly CheckBox _chkTopMost;
    private readonly System.Windows.Forms.Timer _tickTimer;

    public MiniTimerForm(
        ResetConfig config, 
        ConfigService configService, 
        Action onRestoreMain, 
        Func<DateTime> getNextResetTime,
        Func<Task> onPerformReset)
    {
        _config = config;
        _configService = configService;
        _onRestoreMain = onRestoreMain;
        _getNextResetTime = getNextResetTime;
        _onPerformReset = onPerformReset;

        Text = "RadioTEDU Mini Sayacı";
        BackColor = Color.White; // White Mode
        ForeColor = Color.FromArgb(15, 23, 42);
        ClientSize = new Size(420, 200);
        MinimumSize = new Size(280, 140);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        DoubleBuffered = true;

        // Top Info Header
        var appCount = _config.TargetApps.Count;
        var headerTitle = appCount > 1 
            ? $"📻 Çoklu Takip: {appCount} Uygulama | Reset Saati: {_config.TargetHour:D2}:{_config.TargetMinute:D2}"
            : (appCount == 1 
                ? $"📻 {_config.TargetApps[0].Name} | Hedef Reset: {_config.TargetHour:D2}:{_config.TargetMinute:D2}"
                : $"📻 Takip Edilen Uygulama Yok | Reset Saati: {_config.TargetHour:D2}:{_config.TargetMinute:D2}");

        _lblHeader = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = headerTitle
        };

        // Big Scalable Countdown
        _lblCountdown = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Consolas", 36F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 163, 74), // Emerald Green
            Text = "00:00:00"
        };
        _lblCountdown.DoubleClick += (s, e) => _onRestoreMain();

        // Bottom Status Info
        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            Font = new Font("Consolas", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(2, 132, 199),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "● İzleme Aktif"
        };

        // Bottom Action Bar
        _pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            BackColor = Color.FromArgb(241, 245, 249), // Slate 100
            Padding = new Padding(4)
        };

        _btnRestore = new Button
        {
            Text = "📻 Ana Panel",
            Width = 105,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(37, 99, 235), // Royal Blue
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnRestore.FlatAppearance.BorderSize = 0;
        _btnRestore.Click += (s, e) => _onRestoreMain();

        _btnResetNow = new Button
        {
            Text = "⚡ Reset",
            Width = 85,
            Height = 28,
            Left = 115,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(217, 119, 6), // Amber
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnResetNow.FlatAppearance.BorderSize = 0;
        _btnResetNow.Click += async (s, e) =>
        {
            if (MessageBox.Show("Takip edilen tüm uygulamaları hemen yeniden başlatmak istiyor musunuz?", "Manuel Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _btnResetNow.Enabled = false;
                await _onPerformReset();
                _btnResetNow.Enabled = true;
            }
        };

        _chkTopMost = new CheckBox
        {
            Text = "📌 Üstte Tut",
            Left = 210,
            Top = 6,
            Width = 110,
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        };
        _chkTopMost.CheckedChanged += (s, e) => TopMost = _chkTopMost.Checked;

        _pnlBottom.Controls.Add(_btnRestore);
        _pnlBottom.Controls.Add(_btnResetNow);
        _pnlBottom.Controls.Add(_chkTopMost);

        Controls.Add(_lblCountdown);
        Controls.Add(_lblStatus);
        Controls.Add(_pnlBottom);
        Controls.Add(_lblHeader);

        // Resize event for dynamic font scaling
        Resize += (s, e) => UpdateCountdownFontSize();

        // 1s Tick Timer
        _tickTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _tickTimer.Tick += (s, e) => UpdateTimerDisplay();
        _tickTimer.Start();

        UpdateTimerDisplay();
        UpdateCountdownFontSize();
    }

    private void UpdateCountdownFontSize()
    {
        if (_lblCountdown == null) return;

        int availableWidth = Math.Max(100, ClientSize.Width - 30);
        int availableHeight = Math.Max(40, ClientSize.Height - 90);

        float sizeByWidth = availableWidth / 6.2f;
        float sizeByHeight = availableHeight * 0.70f;

        float optimalSize = Math.Max(12f, Math.Min(sizeByWidth, sizeByHeight));

        _lblCountdown.Font = new Font("Consolas", optimalSize, FontStyle.Bold);
    }

    public void UpdateTimerDisplay()
    {
        var nextTime = _getNextResetTime();
        var remaining = nextTime - DateTime.Now;

        if (remaining.TotalSeconds <= 0)
        {
            _lblCountdown.Text = "RESTARTING...";
            _lblCountdown.ForeColor = Color.FromArgb(220, 38, 38); // Red
            _lblStatus.Text = "⏰ Zamanı Geldi: Otomatik Yeniden Başlatılıyor...";
            return;
        }

        int hours = (int)remaining.TotalHours;
        _lblCountdown.Text = $"{hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";

        // Color coding for light theme
        if (remaining.TotalMinutes < 30)
        {
            _lblCountdown.ForeColor = Color.FromArgb(220, 38, 38); // Red
        }
        else if (remaining.TotalHours < 2)
        {
            _lblCountdown.ForeColor = Color.FromArgb(217, 119, 6); // Amber
        }
        else
        {
            _lblCountdown.ForeColor = Color.FromArgb(22, 163, 74); // Emerald Green
        }

        // Check multi-process status
        var activeApps = _config.TargetApps.Where(a => a.Enabled).ToList();
        if (activeApps.Count == 0)
        {
            _lblStatus.Text = $"⚠️ Takip Edilen Aktif Uygulama Yok | Sonraki: {nextTime:dd.MM HH:mm}";
            _lblStatus.ForeColor = Color.FromArgb(100, 116, 139);
            return;
        }

        var statuses = ProcessManager.GetStatuses(activeApps);
        int runningCount = statuses.Count(s => s.IsRunning);
        double totalRamMb = statuses.Where(s => s.IsRunning).Sum(s => s.MemoryMb);

        if (runningCount == activeApps.Count)
        {
            _lblStatus.Text = $"● TÜMÜ AKTİF ({runningCount}/{activeApps.Count} | RAM: {totalRamMb:F1} MB) | Sonraki: {nextTime:dd.MM HH:mm}";
            _lblStatus.ForeColor = Color.FromArgb(2, 132, 199);
        }
        else if (runningCount > 0)
        {
            _lblStatus.Text = $"▲ KISMİ AKTİF ({runningCount}/{activeApps.Count} Çalışıyor) | Sonraki: {nextTime:dd.MM HH:mm}";
            _lblStatus.ForeColor = Color.FromArgb(217, 119, 6);
        }
        else
        {
            _lblStatus.Text = $"■ TÜMÜ KAPALI / ÇÖKTÜ (0/{activeApps.Count}) | Sonraki: {nextTime:dd.MM HH:mm}";
            _lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }
}
