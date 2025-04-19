namespace TgStatsApp.Settings;

public record DayweekStatsSettingItem
{
    public string Label { get; set; }

    public string Day { get; set; }

    public DayOfWeek? DayOfWeek =>
        Enum.TryParse(Day, out DayOfWeek dayOfWeek)
            ? dayOfWeek
            : null;
}