namespace TgStatsApp.Helpers;

public record WeekdayToRu
{
    private readonly DayOfWeek _source;
    private readonly string _result;

    public WeekdayToRu(DayOfWeek source)
    {
        _source = source;
        _result = source switch
        {
            DayOfWeek.Monday => "Понедельник",
            DayOfWeek.Tuesday => "Вторник",
            DayOfWeek.Wednesday => "Среда",
            DayOfWeek.Thursday => "Четверг",
            DayOfWeek.Friday => "Пятница",
            DayOfWeek.Saturday => "Суббота",
            DayOfWeek.Sunday => "Воскресенье",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };
    }

    public override string ToString()
    {
        return _result;
    }

    public static implicit operator string(WeekdayToRu weekdayToRu)
    {
        return weekdayToRu._result;
    }
}