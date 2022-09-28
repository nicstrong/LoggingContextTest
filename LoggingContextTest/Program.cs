using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341", apiKey: "jgyNjzfnVwpyGCwNxDgs")
    .CreateLogger();

try
{
    Log.Information("Starting host");
    await CreateHostBuilder(args).RunConsoleAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static IHostBuilder CreateHostBuilder(string[] args)
{
    return Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
            services.AddLogging();
            services.AddHostedService<TestHostedService>();
            services.AddScoped<FakeController>();
            services.AddScoped<IFakeService, FakeService>();
        })
        .UseSerilog();
}

public class TestHostedService : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<TestHostedService> _logger;

    public TestHostedService(IServiceScopeFactory serviceScopeFactory, ILogger<TestHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling requests");
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            using var scope = _serviceScopeFactory.CreateScope();
            _logger.LogInformation("Simulating request after 15 secs");
            await SimulateRequest(scope, cancellationToken);
        }
    }

    private static async Task SimulateRequest(IServiceScope serviceScope, CancellationToken cancellationToken)
    {
        var loggerFactory = serviceScope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<TestHostedService>();

        var requestId = Guid.NewGuid().ToString();
        using var requestLoggingScope = logger.BeginScope(new Dictionary<string, object>() { ["RequestId"] = requestId });
        var controller = serviceScope.ServiceProvider.GetRequiredService<FakeController>();
        logger.LogInformation("Handling fake request to FakeController");
        await controller.FakeRequest(cancellationToken);
        logger.LogInformation("Fake request to FakeController complete");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public class FakeController
{
    private readonly ILogger<FakeController> _logger;
    private readonly IFakeService _fakeService;

    public FakeController(ILogger<FakeController> logger, IFakeService fakeService)
    {
        _logger = logger;
        _fakeService = fakeService;
    }

    public async Task FakeRequest(CancellationToken cancellationToken)
    {
        using var scope1 = _logger.BeginScope(new Dictionary<string, object>() { ["FakeService"] = true });
        using var scope2 = _logger.BeginScope(new Dictionary<string, object>() { ["Location"] = "FakeController" });

        _logger.LogInformation("Starting fake request");

        await _fakeService.ServiceCall(cancellationToken);

        _logger.LogInformation("Ending fake request");
    }
}

public interface IFakeService
{
    Task ServiceCall(CancellationToken cancellationToken);
}

public class FakeService : IFakeService
{
    private readonly ILogger _logger;

    public FakeService(ILogger<FakeService> logger)
    {
        _logger = logger;
    }

    public async Task ServiceCall(CancellationToken cancellationToken)
    {
        using var scope2 = _logger.BeginScope(new Dictionary<string, object>() { ["Location"] = "FakeService" });

        _logger.LogInformation("Starting fake service call");

        await Task.Delay(2000, cancellationToken);

        _logger.LogInformation("Fake service call complete");
    }
}