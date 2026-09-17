using System.Drawing;
using System.Windows.Forms;

namespace RadioTEDU.AutoReset.Forms;

public class AlertPopupForm : Form
{
    private int _secondsRemaining = 5;
    private readonly System.Windows.Forms.Timer _autoCloseTimer;
    private readonly Label _lblCountdown;

    protected override bool ShowWithoutActivation => true;

    public AlertPopupForm(string appName, string message)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(380, 125);
        BackColor = Color.FromArgb(37, 99, 235); // Accent blue border
        ForeColor = Color.FromArgb(15, 23, 42);
        DoubleBuffered = true;

        // Position at bottom-right corner
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.AllScreens[0].WorkingArea;
        Location = new Point(workingArea.Right - Width - 20, workingArea.Bottom - Height - 20);

        // Border panel
        var pnlBorder = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12),
            Margin = new Padding(2)
        };

        var lblBadge = new Label
        {
            Text = "🔔 RTAUR KORUMA AKTİF",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(217, 119, 6), // Amber
            Location = new Point(12, 10),
            AutoSize = true
        };

        var lblTitle = new Label
        {
            Text = "RTAUR Tarafından Geri Açıldı!",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 163, 74), // Emerald Green
            Location = new Point(12, 30),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = $"'{appName}' uygulamasının kapandığı algılandı ve otomatik olarak yeniden çalıştırıldı.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(51, 65, 85),
            Location = new Point(12, 56),
            Size = new Size(350, 36)
        };

        _lblCountdown = new Label
        {
            Text = "5s sonra kapanacak",
            Font = new Font("Consolas", 8F),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(12, 98),
            AutoSize = true
        };

        var btnClose = new Button
        {
            Text = "Kapat",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(15, 23, 42),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(300, 93),
            Width = 65,
            Height = 22,
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => Close();

        pnlBorder.Controls.Add(lblBadge);
        pnlBorder.Controls.Add(lblTitle);
        pnlBorder.Controls.Add(lblDesc);
        pnlBorder.Controls.Add(_lblCountdown);
        pnlBorder.Controls.Add(btnClose);

        Controls.Add(pnlBorder);

        // Click anywhere to dismiss
        Click += (s, e) => Close();
        pnlBorder.Click += (s, e) => Close();

        // 1-second timer to auto-close
        _autoCloseTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _autoCloseTimer.Tick += (s, e) =>
        {
            _secondsRemaining--;
            if (_secondsRemaining <= 0)
            {
                _autoCloseTimer.Stop();
                Close();
            }
            else
            {
                _lblCountdown.Text = $"{_secondsRemaining}s sonra kapanacak";
            }
        };
        _autoCloseTimer.Start();
    }

    public static void ShowPopup(string appName, string message)
    {
        try
        {
            var popup = new AlertPopupForm(appName, message);
            popup.Show();
        }
        catch { }
    }
}
