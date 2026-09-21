using Serilog;
using VisaTelegramBot.Application;
using VisaTelegramBot.Infrastructure;
using VisaTelegramBot.Worker.BackgroundJobs;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddTelegramPublishing(builder.Configuration);

    builder.Services.AddOptions<FetchSchedulerOptions>()
        .Bind(builder.Configuration.GetSection(FetchSchedulerOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<DeliveryProcessorOptions>()
        .Bind(builder.Configuration.GetSection(DeliveryProcessorOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddHostedService<NewsFetchScheduler>();
    builder.Services.AddHostedService<ChannelDeliveryProcessor>();

    // Graceful shutdown: kapanis sinyali geldiginde yarim kalan islere tamamlanmalari icin sure taninir.
    builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));

    var host = builder.Build();

    await host.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Worker beklenmedik şekilde sonlandı.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
