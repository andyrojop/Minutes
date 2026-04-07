using Microsoft.Extensions.Configuration;

namespace Project_Minutes.Configuration;

public sealed class AppConfiguration
{
    public required string MeetingMinutesConnectionString { get; init; }
    public int CommandTimeoutSeconds { get; init; } = 30;

    public static AppConfiguration Load(string? basePath = null)
    {
        basePath ??= AppDomain.CurrentDomain.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var cs = config.GetConnectionString("MeetingMinutes")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:MeetingMinutes en appsettings.json.");

        var timeout = config.GetSection("Database").GetValue<int?>("CommandTimeoutSeconds") ?? 30;

        return new AppConfiguration
        {
            MeetingMinutesConnectionString = cs,
            CommandTimeoutSeconds = timeout
        };
    }
}
