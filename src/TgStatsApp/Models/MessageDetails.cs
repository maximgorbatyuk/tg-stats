﻿using TdLib;
using TgStatsApp.Helpers;

namespace TgStatsApp.Models;

public record MessageDetails
{
    private readonly string _messageIdAsString;

    public MessageDetails(
        TdApi.Message message)
    {
        Message = message;
        Date = new UnixDate(message.Date).DateTime;
        ViewsCount = message.InteractionInfo?.ViewCount ?? 0;
        RepliesCount = message.InteractionInfo?.ReplyInfo?.ReplyCount ?? 0;
        _messageIdAsString = message.Id.ToString();
    }

    public TdApi.Message Message { get; }

    public DateTime Date { get; }

    public bool IsWednesdayPost => Date.DayOfWeek == DayOfWeek.Wednesday;

    public bool IsThursdayPost => Date.DayOfWeek == DayOfWeek.Thursday;

    public int ViewsCount { get; }

    public int RepliesCount { get; }
}