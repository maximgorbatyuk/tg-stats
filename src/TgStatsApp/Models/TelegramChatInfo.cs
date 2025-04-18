using TdLib;

namespace TgStatsApp.Models;

public record TelegramChatInfo
{
    public TelegramChatInfo(
        TdApi.Chat chat)
    {
        Id = chat.Id;
        Title = chat.Title;
    }

    public long Id { get; }

    public string Title { get; }

    public string AsSelectOption =>
        $"{Title} ({Id})";
}