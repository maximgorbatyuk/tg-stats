// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TgStatsApp.RequestProcessors;

namespace TgStatsApp;

public abstract class Program
{
    private const string GithubLink = "https://github.com/maximgorbatyuk/tg-stats";

    private const string AuthorizeInTG = "1. Авторизация в телеграм";
    private const string GetChannelsList = "2. Список каналов";
    private const string ExitOption = "Выход";
    
    public static async Task Main(
        string[] args)
    {
        AnsiConsole.Write(
            new FigletText("TG Stats 3000")
                .Color(Color.Green));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(
                Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true)
            .Build();
        
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(configure =>
        {
            configure.AddConsole();
            configure.SetMinimumLevel(LogLevel.Information);
        });

        using var tgClient = new TelegramApiWrapper(
            configuration,
            serviceCollection.BuildServiceProvider().GetService<ILogger<TelegramApiWrapper>>());

        AnsiConsole.WriteLine("TG Stats 3000 приветствует вас!");
        AnsiConsole.MarkupLine("Это приложение для получения статистики по выбранному каналу в Telegram.");
        AnsiConsole.MarkupLine($"Исходный код доступен по ссылке: {GithubLink}");
        AnsiConsole.MarkupLine("Для начала вам необходимо авторизоваться с помощью телефона, кода верификации и пароля.");

        var exitRequested = false;
        do
        {
            var selectedOption = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Что вы хотите сделать? Выберите [green]опцию:[/]?")
                .AddChoices(
                    AuthorizeInTG,
                    GetChannelsList,
                    ExitOption));

            switch (selectedOption)
            {
                case AuthorizeInTG:
                    var initializeResult = await tgClient.InitializeAsync();
                    if (!initializeResult)
                    {
                        AnsiConsole.MarkupLine("[red]Ошибка при инициализации библиотеки![/]");
                        continue;
                    }

                    await tgClient.LoginAsync();
                    continue;
                
                case GetChannelsList:
                    var channels = await tgClient.GetChannelsListAsync();
                    if (channels.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]У вас нет подписок на каналы![/]");
                        continue;
                    }

                    var selectedChat = AnsiConsole.Prompt(new SelectionPrompt<string>()
                        .Title("Выберите канал для формирования статистики:")
                        .AddChoices(channels
                            .Select(c => c.AsSelectOption)));

                    var selectedChannel = channels
                        .FirstOrDefault(c => c.AsSelectOption == selectedChat);
                    
                    if (selectedChannel == null)
                    {
                        AnsiConsole.MarkupLine("[red]Ошибка при выборе канала![/]");
                        continue;
                    }

                    await new GetStatsCommandProcessor(tgClient)
                        .Handle(selectedChannel);

                    continue;

                case ExitOption:
                    AnsiConsole.MarkupLine("[yellow]Выход...[/]");
                    await tgClient.LogoutAsync();
                    exitRequested = true;
                    break;
            }
        } while (!exitRequested);

        AnsiConsole.WriteLine("Прощайте!");
    }
}