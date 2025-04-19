using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TgStatsApp.Settings;

public record AppConfiguration
{
    public AppConfiguration(
        List<DayweekStatsSettingItem> dayweekStats,
        int telegamAppId,
        string telegamAppHash,
        IServiceProvider serviceProvider)
    {
        DayweekStats = dayweekStats;
        TelegamAppId = telegamAppId;
        TelegamAppHash = telegamAppHash;
        ServiceProvider = serviceProvider;
    }

    public List<DayweekStatsSettingItem> DayweekStats { get; }

    public int TelegamAppId { get; }

    public string TelegamAppHash { get; }

    public IServiceProvider ServiceProvider { get; }

    public static AppConfiguration Initialize()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(
                Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true)
            .Build();

        var appId = configuration.GetValue<int>("Telegram:AppId");
        var apiHash = configuration["Telegram:AppHash"];
        var dayweekStats = configuration
            .GetSection("DayweekStats")
            .Get<List<DayweekStatsSettingItem>>();

        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddLogging(configure =>
            {
                configure.AddConsole();
                configure.SetMinimumLevel(LogLevel.Information);
            });

        var serviceProvider = serviceCollection.BuildServiceProvider();

        return new AppConfiguration(
            dayweekStats,
            appId,
            apiHash,
            serviceProvider);
    }
}