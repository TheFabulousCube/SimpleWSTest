using ClientWorkerService;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;

var logger = new LoggerConfiguration()
    .WriteTo.File("C:\\Logs\\Serilogs\\log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((hostContext, services) =>
    {
        var config = hostContext.Configuration;

        // Parse command line arguments
        var clientId = GetArgumentValue(args, "--clientId") ?? Environment.GetEnvironmentVariable("ClientId");
        if (string.IsNullOrEmpty(clientId))
        {
            throw new ArgumentException("ClientId must be provided either as a command-line argument or an environment variable.");
        }

        var serverUrl = GetArgumentValue(args, "--serverUrl") ?? config["WebSocketServerUrl"];
        var heartbeatDelayStr = GetArgumentValue(args, "--heartbeatDelay") ?? config["HeartbeatDelay"];
        if (!int.TryParse(heartbeatDelayStr, out int heartbeatDelay))
        {
            heartbeatDelay = 30; // Default value
        }

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddEventLog(new EventLogSettings
            {
                SourceName = "ClientWorkerService"
            });
            logging.AddSerilog(logger); // Add Serilog
            logging.SetMinimumLevel(LogLevel.Information); // Explicitly set minimum log level
        });

        services.AddWindowsService(options =>
        {
            options.ServiceName = "ClientWorkerService";
        });

        services.AddHostedService<Worker>(sp => new Worker(
            sp.GetRequiredService<ILogger<Worker>>(),
            clientId,
            serverUrl,
            heartbeatDelay
        ));
    })
    .Build();

host.Run();

static string GetArgumentValue(string[] args, string key)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == key && i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }
    return null;
}
