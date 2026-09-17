using Spectre.Console;
using RadioTEDU.AutoReset.Models;
using RadioTEDU.AutoReset.Services;

namespace RadioTEDU.AutoReset;

public class ConsoleRenderer
{
    public static void RenderHeader()
    {
        AnsiConsole.Clear();
        var rule = new Rule("[bold cyan]📻 RadioTEDU Auto Reset & Watchdog System[/]");
        rule.Centered();
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine("[dim]Windows 10 7/24 Otomatik Servis Yeniden Başlatıcı ve Bellek Koruyucu[/]\n");
    }

    public static ResetConfig RunWizard(ConfigService configService, ResetConfig? existing = null)
    {
        RenderHeader();
        AnsiConsole.MarkupLine("[bold yellow]⚙️ KURULUM VE YAPILANDIRMA SİHİRBAZI[/]\n");

        var config = existing ?? new ResetConfig();

        // 1. Hedef EXE Konumu
        string defaultPath = !string.IsNullOrEmpty(config.TargetExePath) 
            ? config.TargetExePath 
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "IcecastStreamMonitor", "IcecastStreamMonitor.exe");

        while (true)
        {
            var prompt = new TextPrompt<string>("1️⃣  Lütfen kapatılıp açılacak [bold green].EXE dosyasının tam yolunu[/] girin:")
                .DefaultValue(defaultPath);

            var path = AnsiConsole.Prompt(prompt).Trim('"', ' ');

            if (File.Exists(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                config.TargetExePath = path;
                AnsiConsole.MarkupLine($"[green]✓ Dosya doğrulandı:[/] {Markup.Escape(path)}\n");
                break;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Belirtilen konumda geçerli bir .exe dosyası bulunamadı! Lütfen tekrar deneyin.[/]");
            }
        }

        // 2. Kaç Saatte Bir? (24 veya 48 Saat)
        var intervalChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("2️⃣  Uygulama [bold green]kaç saatlik bir döngüde[/] yeniden başlatılsın?")
                .AddChoices("24 Saat (Her gün)", "48 Saat (İki günde bir)"));

        config.IntervalHours = intervalChoice.StartsWith("24") ? 24 : 48;
        AnsiConsole.MarkupLine($"[green]✓ Döngü:[/] {config.IntervalHours} saat olarak ayarlandı.\n");

        // 3. Saat Kaçta Resetlensin?
        var hourChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("3️⃣  Reset işlemi [bold green]saat kaçta[/] gerçekleşsin?")
                .AddChoices(
                    "03:00 (Önerilen - Gece 3)",
                    "04:00 (Gece 4)",
                    "05:00 (Sabah 5)",
                    "00:00 (Gece Yarısı)",
                    "06:00 (Sabah 6)",
                    "Özel Saat Gir"));

        if (hourChoice.StartsWith("Özel"))
        {
            int customHour = AnsiConsole.Prompt(
                new TextPrompt<int>("Özel saat girin (0 - 23 arası):")
                    .Validate(h => h >= 0 && h <= 23 ? ValidationResult.Success() : ValidationResult.Error("Saat 0 ile 23 arasında olmalıdır!")));
            config.TargetHour = customHour;
        }
        else
        {
            config.TargetHour = int.Parse(hourChoice.Substring(0, 2));
        }

        config.TargetMinute = 0;
        AnsiConsole.MarkupLine($"[green]✓ Hedef Saat:[/] {config.TargetHour:D2}:{config.TargetMinute:D2}\n");

        // 4. 1 Haftalık Önizleme Planı
        DisplaySchedulePreview(config);

        if (AnsiConsole.Confirm("Yukarıdaki planı onaylayıp izlemeyi başlatmak istiyor musunuz?", true))
        {
            configService.SaveConfig(config);
            AnsiConsole.MarkupLine("[bold green]✓ Ayarlar kaydedildi! Canlı monitör başlatılıyor...[/]");
            Thread.Sleep(1500);
            return config;
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Sihirbaz yeniden başlatılıyor...[/]");
            Thread.Sleep(1000);
            return RunWizard(configService, config);
        }
    }

    public static void DisplaySchedulePreview(ResetConfig config)
    {
        var previewList = ScheduleEngine.GeneratePreviewSchedule(config, DateTime.Now, 7);

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[cyan]Sıra[/]").Centered());
        table.AddColumn(new TableColumn("[bold green]Reset Tarihi[/]").Centered());
        table.AddColumn(new TableColumn("[bold yellow]Gün[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Planlanan Saat[/]").Centered());
        table.AddColumn(new TableColumn("[dim]Döngü[/]").Centered());

        var trCulture = new System.Globalization.CultureInfo("tr-TR");

        int index = 1;
        foreach (var dt in previewList)
        {
            var dayName = dt.ToString("dddd", trCulture);
            table.AddRow(
                index.ToString(),
                dt.ToString("dd.MM.yyyy"),
                $"[bold yellow]{dayName}[/]",
                $"[bold green]{dt:HH:mm:ss}[/]",
                $"+{config.IntervalHours} saat"
            );
            index++;
        }

        AnsiConsole.MarkupLine("[bold cyan]📅 1 HAFTALIK ÖNİZLEME PLANI[/]");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    public static void DisplayHistory(ConfigService configService)
    {
        AnsiConsole.Clear();
        RenderHeader();
        AnsiConsole.MarkupLine("[bold yellow]📋 ŞİMDİYE KADAR YAPILAN RESETLERİN GEÇMİŞİ[/]\n");

        var logs = configService.LoadHistory();
        if (logs.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]Henüz kayıtlı bir yeniden başlatma işlemi bulunmuyor.[/]");
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn(new TableColumn("[cyan]Tarih & Saat[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold]Tetikleyici[/]").Centered());
            table.AddColumn(new TableColumn("[bold]Durum[/]").Centered());
            table.AddColumn(new TableColumn("[bold]Önceki PID -> Yeni PID[/]").Centered());
            table.AddColumn(new TableColumn("[dim]Mesaj[/]").LeftAligned());

            foreach (var log in logs.Take(25))
            {
                var status = log.Success ? "[green]✓ Başarılı[/]" : "[red]✗ Başarısız[/]";
                var trigger = log.TriggerType switch
                {
                    "Scheduled" => "[cyan]⏰ Otomatik (Zamanlı)[/]",
                    "Manual" => "[yellow]👤 Elle (Kullanıcı)[/]",
                    "WatchdogCrashRecovery" => "[red]🚨 Çökme Kurtarma[/]",
                    _ => log.TriggerType
                };

                table.AddRow(
                    log.Timestamp.ToString("dd.MM.yyyy HH:mm:ss"),
                    trigger,
                    status,
                    $"{log.PreviousPid} ➔ {log.NewPid}",
                    Markup.Escape(log.Message)
                );
            }

            AnsiConsole.Write(table);
        }

        AnsiConsole.MarkupLine("\n[dim]Ana ekrana dönmek için herhangi bir tuşa basın...[/]");
        Console.ReadKey(true);
    }
}
