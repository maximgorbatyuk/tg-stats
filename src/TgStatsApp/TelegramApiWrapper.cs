using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TdLib;
using TgStatsApp.Models;
using TgStatsApp.Settings;
using TdLogLevel = TdLib.Bindings.TdLogLevel;

namespace TgStatsApp;

public class TelegramApiWrapper : IDisposable
{
    private const string AppVersion = "0.1";

    private readonly AppConfiguration _configuration;
    private readonly TdClient _client;
    private readonly ILogger<TelegramApiWrapper> _logger;

    private TdApi.User _user;
    private bool _initialized;

    private string _phoneNumber;
    private string _password;

    public TelegramApiWrapper(
        AppConfiguration configuration)
    {
        _configuration = configuration;
        _logger = _configuration.ServiceProvider.GetRequiredService<ILogger<TelegramApiWrapper>>();
        _client = new TdClient();
        _client.Bindings.SetLogVerbosityLevel((int)TdLogLevel.Fatal);
    }
    
    public async Task<bool> InitializeAsync()
    {
        if (_initialized)
        {
            return true;
        }

        var result = await TryExecuteAsync<bool>(async client =>
        {
            var authorization = await _client.ExecuteAsync(
                new TdApi.SetTdlibParameters
                {
                    ApiId = _configuration.TelegamAppId,
                    ApiHash = _configuration.TelegamAppHash,
                    DeviceModel = "PC",
                    SystemLanguageCode = "en",
                    ApplicationVersion = AppVersion,
                });

            return true;
        });

        _initialized = result.IsSuccess && result.Result;
        return result.Result;
    }

    public async Task<bool> LoginAsync()
    {
        var currentAuthorizationStateResponse = await TryExecuteAsync<TdApi.AuthorizationState>(async client =>
        {
            var authState = await _client.ExecuteAsync(
                new TdApi.GetAuthorizationState());

            return authState;
        });

        if (currentAuthorizationStateResponse.Result is TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters)
        {
            await InitializeAsync();
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

            AnsiConsole.MarkupLine("[green]Пользователь уже авторизован![/]");
            DisplayUser();
            return true;
        }

        if (_phoneNumber == null)
        {
            _phoneNumber = AnsiConsole.Ask<string>("Введите номер телефона ([yellow]+77011112233[/]):")?.Trim();
        }

        if (string.IsNullOrWhiteSpace(_phoneNumber))
        {
            AnsiConsole.MarkupLine("[red]Телефон не указан![/]");
            return false;
        }

        var phoneAuthResult = await TryExecuteAsync<bool>(async client =>
        {
            var phoneAuthentication = await _client.ExecuteAsync(
                new TdApi.SetAuthenticationPhoneNumber
                {
                    PhoneNumber = _phoneNumber
                });

            return true;
        });

        if (!phoneAuthResult.IsSuccess)
        {
            AnsiConsole.MarkupLine("[red]Ошибка при отправке запроса на код верификации![/]");
            return false;
        }

        var phoneCode = AnsiConsole.Ask<string>(
            "Введите код авторизации, отправленный вам телеграмом: ([yellow]12345[/]):")?.Trim();
        
        if (string.IsNullOrWhiteSpace(phoneCode))
        {
            AnsiConsole.MarkupLine("[red]Код не введен![/]");
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
            AnsiConsole.MarkupLine("[red]Ошибка верификации кода![/]");
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
            if (string.IsNullOrEmpty(_password))
            {
                _password = AnsiConsole.Ask<string>(
                    "Введите ваш пароль: ([yellow]password[/]):")?.Trim();
                
                if (string.IsNullOrWhiteSpace(_password))
                {
                    AnsiConsole.MarkupLine("[red]Пароль не введен![/]");
                    return false;
                }
            }

            var passwordResult = await TryExecuteAsync<bool>(async client =>
            {
                var passwordAuthentication = await _client.ExecuteAsync(
                    new TdApi.CheckAuthenticationPassword
                    {
                        Password = _password
                    });

                return true;
            });
            
            if (!passwordResult.IsSuccess ||
                !passwordResult.Result)
            {
                AnsiConsole.MarkupLine("[red]Ошибка верификации пароля![/]");
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
                AnsiConsole.MarkupLine("[green]Аутентификация успешна![/]");
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
            AnsiConsole.MarkupLine("[red]Ошибка при запросе пользовательских данных![/]");
            return false;
        }

        DisplayUser();
        return true;
    }

    public async Task InitializeAppIfNecessaryAsync()
    {
        if (_user == null)
        {
            await InitializeAsync();
            await LoginAsync();
        }
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
        AnsiConsole.MarkupLine($"ID: [green]{_user.Id}[/]");
        AnsiConsole.MarkupLine($"Телефон: [green]{_user.PhoneNumber}[/]");
        AnsiConsole.MarkupLine($"Username: [green]{_user.Usernames.ActiveUsernames.FirstOrDefault()}[/]");
        AnsiConsole.WriteLine();
    }

    public void Dispose()
    {
        _client?.CloseAsync().Wait();
        _client?.Dispose();
    }
}