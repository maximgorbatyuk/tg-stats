using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TdLib;
using TgStatsApp.Models;
using TdLogLevel = TdLib.Bindings.TdLogLevel;

namespace TgStatsApp;

public class TelegramApiWrapper : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly TdClient _client;
    private readonly ILogger<TelegramApiWrapper> _logger;

    private TdApi.User _user;

    public TelegramApiWrapper(
        IConfiguration configuration,
        ILogger<TelegramApiWrapper> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _client = new TdClient();
        _client.Bindings.SetLogVerbosityLevel((int)TdLogLevel.Fatal);
    }
    
    public async Task<bool> InitializeAsync()
    {
        var appVersion = _configuration["AppVersion"];
        var appId = _configuration.GetValue<int>("Telegram:AppId");
        var apiHash = _configuration["Telegram:AppHash"];

        if (appId == 0 || string.IsNullOrWhiteSpace(apiHash))
        {
            AnsiConsole.MarkupLine("[red]API ID or API Hash is not set![/]");
            return false;
        }

        return await TryExecuteAsync<bool>(async client =>
        {
            var authorization = await _client.ExecuteAsync(
                new TdApi.SetTdlibParameters
                {
                    ApiId = appId,
                    ApiHash = apiHash,
                    DeviceModel = "PC",
                    SystemLanguageCode = "en",
                    ApplicationVersion = appVersion,
                });

            return true;
        });
    }

    public async Task<bool> SendPhoneAuthCodeAsync()
    {
        var currentAuthorizationState = await TryExecuteAsync<TdApi.AuthorizationState>(async client =>
        {
            var authState = await _client.ExecuteAsync(
                new TdApi.GetAuthorizationState());

            return authState;
        });
        
        if (currentAuthorizationState is TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters)
        {
            AnsiConsole.MarkupLine("[red]Please initialize the Telegram API first![/]");
            return false;
        }
        
        if (currentAuthorizationState is TdApi.AuthorizationState.AuthorizationStateReady)
        {
            _user = await TryExecuteAsync<TdApi.User>(async client =>
            {
                var userInfo = await _client.ExecuteAsync(
                    new TdApi.GetMe());

                return userInfo;
            });

            AnsiConsole.MarkupLine("[green]Already authorized![/]");
            DisplayUser();
            return true;
        }

        var phoneNumber = _configuration["UserPhone"]?.Trim();
        if (string.IsNullOrEmpty(phoneNumber))
        {
            phoneNumber = AnsiConsole.Ask<string>(
                "Please enter your phone number ([yellow]+77011112233[/]):")?.Trim();
            
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                AnsiConsole.MarkupLine("[red]Phone number cannot be empty![/]");
                return false;
            }
        }

        var phoneAuthResult = await TryExecuteAsync<bool>(async client =>
        {
            var phoneAuthentication = await _client.ExecuteAsync(
                new TdApi.SetAuthenticationPhoneNumber
                {
                    PhoneNumber = phoneNumber
                });

            return true;
        });

        if (!phoneAuthResult)
        {
            AnsiConsole.MarkupLine("[red]Failed to send phone authentication code![/]");
            return false;
        }

        var phoneCode = AnsiConsole.Ask<string>(
            "Please enter the code sent to your phone ([yellow]12345[/]):")?.Trim();
        
        if (string.IsNullOrWhiteSpace(phoneCode))
        {
            AnsiConsole.MarkupLine("[red]Phone code cannot be empty![/]");
            return false;
        }

        var verificationResult = await TryExecuteAsync<bool>(async client =>
        {
            var phoneAuthentication = await _client.ExecuteAsync(
                new TdApi.CheckAuthenticationCode
                {
                    Code = phoneCode
                });

            return true;
        });
        
        if (!verificationResult)
        {
            AnsiConsole.MarkupLine("[red]Failed to verify phone authentication code![/]");
            return false;
        }

        var authenticationState = await TryExecuteAsync<TdApi.AuthorizationState>(async client =>
        {
            var authState = await _client.ExecuteAsync(
                new TdApi.GetAuthorizationState());

            return authState;
        });

        if (authenticationState is TdApi.AuthorizationState.AuthorizationStateWaitPassword)
        {
            var password = _configuration["UserPassword"]?.Trim();
            if (string.IsNullOrEmpty(password))
            {
                password = AnsiConsole.Ask<string>(
                    "Please enter your password ([yellow]password[/]):")?.Trim();
                
                if (string.IsNullOrWhiteSpace(password))
                {
                    AnsiConsole.MarkupLine("[red]Password cannot be empty![/]");
                    return false;
                }
            }

            var passwordResult = await TryExecuteAsync<bool>(async client =>
            {
                var passwordAuthentication = await _client.ExecuteAsync(
                    new TdApi.CheckAuthenticationPassword
                    {
                        Password = password
                    });

                return true;
            });
            
            if (!passwordResult)
            {
                AnsiConsole.MarkupLine("[red]Failed to verify password![/]");
                return false;
            }

            authenticationState = await TryExecuteAsync<TdApi.AuthorizationState>(async client =>
            {
                var authState = await _client.ExecuteAsync(
                    new TdApi.GetAuthorizationState());

                return authState;
            });
            
            if (authenticationState is TdApi.AuthorizationState.AuthorizationStateReady authReady)
            {
                AnsiConsole.MarkupLine("[green]Password authentication successful![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to verify password![/]");
                return false;
            }
        }

        _user = await TryExecuteAsync<TdApi.User>(async client =>
        {
            var userInfo = await _client.ExecuteAsync(
                new TdApi.GetMe());

            return userInfo;
        });

        if (_user == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to get user information![/]");
            return false;
        }

        DisplayUser();
        return true;
    }

    public async Task<List<TelegramChatInfo>> GetChannelsListAsync()
    {
        if (_user == null)
        {
            throw new InvalidOperationException(
                "User is not authenticated. Please authenticate first.");
        }
        
        var mainChatList = await TryExecuteAsync<TdApi.Chats>(async client =>
            await _client.ExecuteAsync(
                new TdApi.GetChats
                {
                    Limit = 200,
                    ChatList = new TdApi.ChatList.ChatListMain(),
                }));
        
        if (mainChatList == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to get chat list![/]");
            return new List<TelegramChatInfo>(0);
        }

        var result = new List<TelegramChatInfo>();
        foreach (var chatId in mainChatList.ChatIds)
        {
            var chat = await _client.ExecuteAsync(
                new TdApi.GetChat
                {
                    ChatId = chatId
                });

            var chatType = chat.Type;
            if (chatType is TdApi.ChatType.ChatTypeSupergroup)
            {
                result.Add(
                    new TelegramChatInfo(
                        chat.Id,
                        chat.Title));
            }
        }

        var archiveChatList = await TryExecuteAsync<TdApi.Chats>(async client =>
            await _client.ExecuteAsync(
                new TdApi.GetChats
                {
                    Limit = 200,
                    ChatList = new TdApi.ChatList.ChatListArchive(),
                }));

        if (archiveChatList != null && archiveChatList.ChatIds.Length > 0)
        {
            foreach (var chatId in archiveChatList.ChatIds)
            {
                var chat = await _client.ExecuteAsync(
                    new TdApi.GetChat
                    {
                        ChatId = chatId
                    });

                var chatType = chat.Type;
                if (chatType is TdApi.ChatType.ChatTypeSupergroup)
                {
                    result.Add(
                        new TelegramChatInfo(
                            chat.Id,
                            chat.Title));
                }
            }
        }

        return result;
    }

    public async Task GetStatsAsync(
        TelegramChatInfo selectedChannel)
    {
        var messages = new List<TdApi.Message>();
        var channelMessages = await TryExecuteAsync<TdApi.Messages>(async client =>
            await _client.ExecuteAsync(
                new TdApi.GetChatHistory()
                {
                    ChatId = selectedChannel.Id,
                    Limit = 50,
                    Offset = 0,
                }));

        if (channelMessages == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to get chat list![/]");
            return;
        }

        if (channelMessages.Messages_.Length == 1)
        {
            messages.Add(channelMessages.Messages_[0]);

            var moreMessages = await TryExecuteAsync<TdApi.Messages>(async client =>
                await _client.ExecuteAsync(
                    new TdApi.GetChatHistory()
                    {
                        ChatId = selectedChannel.Id,
                        FromMessageId = channelMessages.Messages_[0].Id,
                        Limit = 100,
                        Offset = 0,
                    }));

            if (moreMessages != null && moreMessages.Messages_.Length > 0)
            {
                messages.AddRange(moreMessages.Messages_);
            }
        }

        var messagesToBeProcessed = messages
            .Where(m =>
                m.Content is TdApi.MessageContent.MessageText or TdApi.MessageContent.MessagePhoto)
            .ToList();
        
        var latestMessageDate = new UnixDate(messagesToBeProcessed
            .Select(m => m.Date)
            .Max()).DateTime;

        var earliestMessageDate = new UnixDate(messagesToBeProcessed
            .Select(m => m.Date)
            .Min()).DateTime;

        AnsiConsole.MarkupLine($"[green]Total messages: {messagesToBeProcessed.Count}[/]");
        AnsiConsole.MarkupLine($"[green]Total photos: {messagesToBeProcessed.Count(m => m.Content is TdApi.MessageContent.MessagePhoto)}[/]");
        AnsiConsole.MarkupLine($"[green]Total text messages: {messagesToBeProcessed.Count(m => m.Content is TdApi.MessageContent.MessageText)}[/]");

        AnsiConsole.MarkupLine($"[green]Earliest message date: {earliestMessageDate}[/]");
        AnsiConsole.MarkupLine($"[green]Latest message date: {latestMessageDate}[/]");
    }

    private async Task<TResult> TryExecuteAsync<TResult>(
        Func<TdClient, Task<TResult>> func)
    {
        try
        {
            return await func(_client);
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "Error executing TdLib function");
        }

        return default;
    }
    
    public async Task LogoutAsync()
    {
        if (_user == null)
        {
            return;
        }

        await TryExecuteAsync<bool>(async client =>
        {
            var logOutResult = await _client.ExecuteAsync(
                new TdApi.LogOut());

            return true;
        });
    }

    private void DisplayUser()
    {
        AnsiConsole.MarkupLine($"User ID: [green]{_user.Id}[/]");
        AnsiConsole.MarkupLine($"User Name: [green]{_user.FirstName} {_user.LastName}[/]");
        AnsiConsole.MarkupLine($"Phone Number: [green]{_user.PhoneNumber}[/]");
        AnsiConsole.MarkupLine($"User Type: [green]{_user.Type}[/]");
        AnsiConsole.MarkupLine($"User active nickname: [green]{_user.Usernames.ActiveUsernames.FirstOrDefault()}[/]");
        AnsiConsole.MarkupLine("[green]Phone authentication successful![/]");
    }

    public void Dispose()
    {
        _client?.CloseAsync().Wait();
        _client?.Dispose();
    }
}