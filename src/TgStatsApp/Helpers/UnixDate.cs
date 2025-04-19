namespace TgStatsApp.Helpers;

public record UnixDate
{
    public DateTime DateTime { get; }

    private readonly long _timestamp;

    public UnixDate(
        long timestamp)
    {
        _timestamp = timestamp;
        DateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
    }
}