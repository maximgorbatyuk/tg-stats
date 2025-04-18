// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TgStatsApp.RequestProcessors;

namespace TgStatsApp;

public abstract class Program
{
    private const string ShowSettings = "Show settings";
    private const string AuthorizeInTG = "Authorize in TG";
    private const string GetChannelsList = "Get Channels list";
    private const string ExitOption = "Exit";
    
    public static async Task Main(
        string[] args)
    {
        AnsiConsole.Write(
            new FigletText("TG Stats 3000")
                .Color(Color.Green));

        AnsiConsole.WriteLine("TG Stats 3000 greeting you!");

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
        
        var exitRequested = false;
        do
        {
            var selectedOption = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("What do you want to do? Please select [green]option[/]?")
                .AddChoices(
                    ShowSettings,
                    AuthorizeInTG,
                    GetChannelsList,
                    ExitOption));

            switch (selectedOption)
            {
                case ShowSettings:
                    continue;
                
                case AuthorizeInTG:
                    var initializeResult = await tgClient.InitializeAsync();
                    if (!initializeResult)
                    {
                        AnsiConsole.MarkupLine("[red]Failed to initialize Telegram API![/]");
                        continue;
                    }

                    await tgClient.SendPhoneAuthCodeAsync();
                    continue;
                
                case GetChannelsList:
                    var channels = await tgClient.GetChannelsListAsync();
                    if (channels.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]You have no channels![/]");
                        continue;
                    }

                    var selectedChat = AnsiConsole.Prompt(new SelectionPrompt<string>()
                        .Title("Select a channel to show stats:")
                        .AddChoices(channels
                            .Select(c => c.AsSelectOption)));

                    var selectedChannel = channels
                        .FirstOrDefault(c => c.AsSelectOption == selectedChat);
                    
                    if (selectedChannel == null)
                    {
                        AnsiConsole.MarkupLine("[red]Failed to select channel![/]");
                        continue;
                    }

                    await new GetStatsCommandProcessor(tgClient)
                        .Handle(selectedChannel);

                    continue;

                case ExitOption:
                    AnsiConsole.MarkupLine("[yellow]Exiting...[/]");
                    await tgClient.LogoutAsync();
                    exitRequested = true;
                    break;
            }
        } while (!exitRequested);

        AnsiConsole.WriteLine("Goodbye!");
    }
}