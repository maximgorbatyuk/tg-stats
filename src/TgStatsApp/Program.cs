// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace TgStatsApp;

public abstract class Program
{
    private const string ShowSettings = "Show settings";
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
        
        var exitRequested = false;
        do
        {
            var selectedOption = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("What do you want to do? Please select [green]option[/]?")
                .AddChoices(
                    ShowSettings,
                    ExitOption));

            switch (selectedOption)
            {
                case ShowSettings:
                    continue;
                
                case ExitOption:
                    exitRequested = true;
                    break;
            }
        } while (!exitRequested);

        AnsiConsole.WriteLine("Goodbye!");
    }
}