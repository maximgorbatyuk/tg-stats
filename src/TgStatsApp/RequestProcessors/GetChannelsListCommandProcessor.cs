using Spectre.Console;
using TdLib;
using TgStatsApp.Models;

namespace TgStatsApp.RequestProcessors;

public class GetChannelsListCommandProcessor
{
    private readonly TelegramApiWrapper _client;

    public GetChannelsListCommandProcessor(
        TelegramApiWrapper client)
    {
        _client = client;
    }

    public async Task<List<TelegramChatInfo>> Handle()
    {
        await _client.InitializeAppIfNecessaryAsync();
        
        var mainChatListResponse = await _client.TryExecuteAsync<TdApi.Chats>(async c =>
            await c.ExecuteAsync(
                new TdApi.GetChats
                {
                    Limit = 200,
                    ChatList = new TdApi.ChatList.ChatListMain(),
                }));
        
        if (mainChatListResponse == null ||
            !mainChatListResponse.IsSuccess)
        {
            AnsiConsole.MarkupLine("[red]Ошибка запроса на список каналов/групп![/]");
            return new List<TelegramChatInfo>(0);
        }

        var result = new List<TelegramChatInfo>();
        foreach (var chatId in mainChatListResponse.Result.ChatIds)
        {
            var chat = await _client.TryExecuteAsync(c =>
                c.ExecuteAsync(
                    new TdApi.GetChat
                    {
                        ChatId = chatId
                    }));

            if (!chat.IsSuccess)
            {
                continue;
            }

            var chatType = chat.Result.Type;
            if (chatType is TdApi.ChatType.ChatTypeSupergroup)
            {
                result.Add(new TelegramChatInfo(chat.Result));
            }
        }

        var archiveChatList = await _client.TryExecuteAsync<TdApi.Chats>(c =>
            c.ExecuteAsync(
                new TdApi.GetChats
                {
                    Limit = 200,
                    ChatList = new TdApi.ChatList.ChatListArchive(),
                }));

        if (archiveChatList != null && archiveChatList.Result.ChatIds.Length > 0)
        {
            foreach (var chatId in archiveChatList.Result.ChatIds)
            {
                var chat = await _client.TryExecuteAsync(c =>
                    c.ExecuteAsync(
                        new TdApi.GetChat
                        {
                            ChatId = chatId
                        }));

                if (!chat.IsSuccess)
                {
                    continue;
                }

                var chatType = chat.Result.Type;
                if (chatType is TdApi.ChatType.ChatTypeSupergroup)
                {
                    result.Add(new TelegramChatInfo(chat.Result));
                }
            }
        }

        return result;
    }
}