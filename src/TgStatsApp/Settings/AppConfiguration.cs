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
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true)
            .Build();

        DotNetEnv.Env.TraversePath().Load();

        // For publishing app as self-packaged, you should insert there your env values instead of reading them from ENV.
        var appIdSource = Environment.GetEnvironmentVariable("TELEGRAM_APP_ID");
        var apiHash = Environment.GetEnvironmentVariable("TELEGRAM_APP_HASH");

        if (appIdSource == null || apiHash == null)
        {
            throw new InvalidOperationException("Missing environment variables TELEGRAM_APP_ID and TELEGRAM_APP_HASH");
        }

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
            int.Parse(appIdSource),
            apiHash,
            serviceProvider);
    }
}