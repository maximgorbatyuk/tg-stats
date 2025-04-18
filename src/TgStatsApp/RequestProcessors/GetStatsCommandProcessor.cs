using Spectre.Console;
using TdLib;
using TgStatsApp.Helpers;
using TgStatsApp.Models;

namespace TgStatsApp.RequestProcessors;

public class GetStatsCommandProcessor
{
    private readonly TelegramApiWrapper _client;

    public GetStatsCommandProcessor(
        TelegramApiWrapper client)
    {
        _client = client;
    }

    public async Task Handle(
        TelegramChatInfo selectedChannel)
    {
        var messages = await GetMessagesForThisMonthAsync(selectedChannel);

        var averagePostsPerDay = messages
            .GroupBy(m => m.Date.Date)
            .Select(g => g.Count())
            .Average();

        AnsiConsole.MarkupLine($"Всего постов за месяц: [green]{messages.Count}[/]");
        if (messages.Count == 0)
        {
            return;
        }
        
        var wednesdayPosts = messages
            .Where(m => m.IsWednesdayPost)
            .ToList();

        var thursdayPosts = messages
            .Where(m => m.IsThursdayPost)
            .ToList();

        AnsiConsole.MarkupLine($"В среднем в день публиковали [green]{averagePostsPerDay:F2}[/] постов");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"Смешных мемов: [yellow]0[/], непонятных: [yellow]{messages.Count - 1}[/]");
        AnsiConsole.MarkupLine($"Постов про среду и лягушку: [green]{wednesdayPosts.Count}[/]");
        AnsiConsole.MarkupLine($"Постов про Марину (четверг): [green]{thursdayPosts.Count}[/]");
        AnsiConsole.WriteLine();

        var groupedByWeekdays = messages
            .GroupBy(m => m.Date.DayOfWeek)
            .OrderByDescending(x => x.Count())
            .ToList();

        var mostActiveWeekdaySource = new List<(DayOfWeek DayOfWeek, double Count)>();
        foreach (var weekGroup in groupedByWeekdays)
        {
            var averagePostsPerMostActiveWeekday = messages
                .Where(x => x.Date.DayOfWeek == weekGroup.Key)
                .GroupBy(x => x.Date.Date)
                .Average(x => x.Count());

            mostActiveWeekdaySource.Add((weekGroup.Key, averagePostsPerMostActiveWeekday));
        }

        mostActiveWeekdaySource = mostActiveWeekdaySource
            .OrderByDescending(x => x.Count)
            .ToList();

        AnsiConsole.MarkupLine($"Самый активный день недели [green]{new WeekdayToRu(mostActiveWeekdaySource[0].DayOfWeek)}[/] - {mostActiveWeekdaySource[0].Count:F2} в среднем");
        AnsiConsole.MarkupLine($"Наименее активный день недели: [yellow]{new WeekdayToRu(mostActiveWeekdaySource[^1].DayOfWeek)}[/] - {mostActiveWeekdaySource[^1].Count:F2} в среднем");
        AnsiConsole.WriteLine();

        var mostViewedPost = messages
            .OrderByDescending(m => m.ViewsCount)
            .FirstOrDefault();

        if (mostViewedPost != null)
        {
            var link = await _client.GetMessageLinkAsync(
                selectedChannel.Id,
                mostViewedPost.Message.Id);

            AnsiConsole.MarkupLine($"Больше всего просмотров ({mostViewedPost!.ViewsCount}) у поста [green]{link}[/]");
        }

        var mostCommentedPost = messages
            .OrderByDescending(m => m.RepliesCount)
            .FirstOrDefault();

        if (mostCommentedPost != null)
        {
            var link = await _client.GetMessageLinkAsync(
                selectedChannel.Id,
                mostCommentedPost.Message.Id);

            AnsiConsole.MarkupLine($"Больше всего комментариев ({mostCommentedPost!.RepliesCount}) у поста [green]{link}[/]");
        }

        AnsiConsole.WriteLine();
    }

    private async Task<List<MessageDetails>> GetMessagesForThisMonthAsync(
        TelegramChatInfo selectedChannel)
    {
        var now = DateTime.UtcNow;
        var startOfTheMonth = new DateTime(
            now.Year,
            now.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var messages = new List<MessageDetails>();
        
        var channelMessagesResponse = await _client.TryExecuteAsync<TdApi.Messages>(async client =>
            await client.ExecuteAsync(
                new TdApi.GetChatHistory
                {
                    ChatId = selectedChannel.Id,
                    Limit = 50,
                    Offset = 0,
                }));

        if (!channelMessagesResponse.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[red]Failed to get chat list! {channelMessagesResponse.ErrorMessage}[/]");
            return messages;
        }

        messages.Add(
            new MessageDetails(channelMessagesResponse.Result.Messages_[0]));

        if (messages.Count != 1)
        {
            return messages;
        }

        var lastProcessedMessageId = messages[0].Message.Id;
        const int limit = 100;

        var exitRequested = false;

        do
        {
            var moreMessages = await _client.TryExecuteAsync<TdApi.Messages>(async client =>
                await client.ExecuteAsync(
                    new TdApi.GetChatHistory()
                    {
                        ChatId = selectedChannel.Id,
                        FromMessageId = lastProcessedMessageId,
                        Limit = limit,
                        Offset = 0,
                    }));

            if (moreMessages == null ||
                moreMessages.Result.Messages_.Length == 0)
            {
                continue;
            }

            foreach (var message in moreMessages.Result.Messages_)
            {
                var messageDate = new UnixDate(message.Date).DateTime;
                if (messageDate < startOfTheMonth)
                {
                    exitRequested = true;
                    break;
                }
                
                messages.Add(new MessageDetails(message));
                lastProcessedMessageId = message.Id;
            }
        }
        while (!exitRequested);

        return messages
            .OrderBy(m => m.Date)
            .ToList();
    }
}