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

        var result = await TryExecuteAsync<bool>(async client =>
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

        return result.Result;
    }

    public async Task<bool> SendPhoneAuthCodeAsync()
    {
        var currentAuthorizationStateResponse = await TryExecuteAsync<TdApi.AuthorizationState>(async client =>
        {
            var authState = await _client.ExecuteAsync(
                new TdApi.GetAuthorizationState());

            return authState;
        });
        
        if (currentAuthorizationStateResponse.Result is TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters)
        {
            AnsiConsole.MarkupLine("[red]Please initialize the Telegram API first![/]");
            return false;
        }
        
        if (currentAuthorizationStateResponse.Result is TdApi.AuthorizationState.AuthorizationStateReady)
        {
            var getUserResult = await TryExecuteAsync<TdApi.User>(async client =>
            {
                var userInfo = await _client.ExecuteAsync(
                    new TdApi.GetMe());

                return userInfo;
            });

            _user = getUserResult.Result;

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

        if (!phoneAuthResult.IsSuccess)
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
        
        if (!verificationResult.IsSuccess)
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

        if (authenticationState.Result is TdApi.AuthorizationState.AuthorizationStateWaitPassword)
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
            
            if (!passwordResult.IsSuccess ||
                !passwordResult.Result)
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
            
            if (authenticationState.Result is TdApi.AuthorizationState.AuthorizationStateReady authReady)
            {
                AnsiConsole.MarkupLine("[green]Password authentication successful![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Failed to verify password![/]");
                return false;
            }
        }
        
        var userResponse = await TryExecuteAsync<TdApi.User>(async client =>
        {
            var userInfo = await _client.ExecuteAsync(
                new TdApi.GetMe());

            return userInfo;
        });

        _user = userResponse.Result;

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
        
        var mainChatListResponse = await TryExecuteAsync<TdApi.Chats>(async client =>
            await _client.ExecuteAsync(
                new TdApi.GetChats
                {
                    Limit = 200,
                    ChatList = new TdApi.ChatList.ChatListMain(),
                }));
        
        if (mainChatListResponse == null ||
            !mainChatListResponse.IsSuccess)
        {
            AnsiConsole.MarkupLine("[red]Failed to get chat list![/]");
            return new List<TelegramChatInfo>(0);
        }

        var result = new List<TelegramChatInfo>();
        foreach (var chatId in mainChatListResponse.Result.ChatIds)
        {
            var chat = await _client.ExecuteAsync(
                new TdApi.GetChat
                {
                    ChatId = chatId
                });

            var chatType = chat.Type;
            if (chatType is TdApi.ChatType.ChatTypeSupergroup)
            {
                result.Add(new TelegramChatInfo(chat));
            }
        }

        var archiveChatList = await TryExecuteAsync<TdApi.Chats>(async client =>
            await _client.ExecuteAsync(
                new TdApi.GetChats
                {
                    Limit = 200,
                    ChatList = new TdApi.ChatList.ChatListArchive(),
                }));

        if (archiveChatList != null && archiveChatList.Result.ChatIds.Length > 0)
        {
            foreach (var chatId in archiveChatList.Result.ChatIds)
            {
                var chat = await _client.ExecuteAsync(
                    new TdApi.GetChat
                    {
                        ChatId = chatId
                    });

                var chatType = chat.Type;
                if (chatType is TdApi.ChatType.ChatTypeSupergroup)
                {
                    result.Add(new TelegramChatInfo(chat));
                }
            }
        }

        return result;
    }

    public async Task<string> GetMessageLinkAsync(
        long chatId,
        long messageId)
    {
        var result = await TryExecuteAsync(c =>
            c.ExecuteAsync(new TdApi.GetMessageLink()
            {
                ChatId = chatId,
                MessageId = messageId,
            }));
        
        if (result.IsSuccess)
        {
            return result.Result.Link;
        }

        return null;
    }

    public async Task<ApiResponse<TResult>> TryExecuteAsync<TResult>(
        Func<TdClient, Task<TResult>> func)
    {
        try
        {
            var result = await func(_client);
            return new ApiResponse<TResult>(result, null);
        }
        catch (TdLib.TdException ex)
        {
            _logger.LogError(
                ex,
                "Telegram API error: {ErrorMessage}",
                ex.Message);

            return new ApiResponse<TResult>(default, ex.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "Error executing TdLib function");

            return new ApiResponse<TResult>(default, e.Message);
        }
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