namespace TgStatsApp.Models;

public record TelegramChatInfo
{
    public TelegramChatInfo(
        long id,
        string title)
    {
        Id = id;
        Title = title;
    }

    public long Id { get; }

    public string Title { get; }

    public string AsSelectOption =>
        $"{Title} ({Id})";
}