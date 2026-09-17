using RadioTEDU.AutoReset.Models;

namespace RadioTEDU.AutoReset.Services;

public class ScheduleEngine
{
    public static DateTime CalculateNextReset(ResetConfig config, DateTime fromTime)
    {
        // Target time of day today
        var targetTimeToday = new DateTime(fromTime.Year, fromTime.Month, fromTime.Day, config.TargetHour, config.TargetMinute, 0);

        if (config.IntervalHours == 48)
        {
            // If LastResetTime is known, step by 48h from that base
            if (config.LastResetTime.HasValue)
            {
                var next = config.LastResetTime.Value.AddHours(48);
                while (next <= fromTime)
                {
                    next = next.AddHours(48);
                }
                return next;
            }

            // If no previous reset recorded, find the next occurrence of TargetHour:TargetMinute
            var candidate = targetTimeToday > fromTime ? targetTimeToday : targetTimeToday.AddDays(1);
            return candidate;
        }
        else // 24 Hours default
        {
            if (targetTimeToday > fromTime)
            {
                return targetTimeToday;
            }
            return targetTimeToday.AddDays(1);
        }
    }

    public static List<DateTime> GeneratePreviewSchedule(ResetConfig config, DateTime startTime, int days = 7)
    {
        var schedule = new List<DateTime>();
        var current = CalculateNextReset(config, startTime);
        var cutoff = startTime.AddDays(days);

        while (current <= cutoff && schedule.Count < 20)
        {
            schedule.Add(current);
            current = current.AddHours(config.IntervalHours);
        }

        return schedule;
    }

    public static string FormatRemainingTime(TimeSpan remaining)
    {
        if (remaining.TotalSeconds <= 0) return "00:00:00 (Yeniden başlatılıyor...)";
        int hours = (int)remaining.TotalHours;
        return $"{hours:D2} saat {remaining.Minutes:D2} dakika {remaining.Seconds:D2} saniye";
    }
}
