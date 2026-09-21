using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using Serilog;
using VisaTelegramBot.Application;
using VisaTelegramBot.Infrastructure;
using VisaTelegramBot.Infrastructure.Persistence;
using VisaTelegramBot.WebApi.Extensions;
using VisaTelegramBot.WebApi.OpenApi;
using VisaTelegramBot.WebApi.Seed;

// Uygulama daha ayaga kalkmadan olusan hatalar da loglansin diye gecici bir logger.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // Composition Root: tum katmanlar burada birbirine baglanir.
    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    // API mesaj gondermez ama panelde kanal adi ve abone sayisini gosterebilmek icin Telegram'a baglanir.
    // Token tanimli degilse API yine calisir; kanal bilgisi bos gelir.
    if (!string.IsNullOrWhiteSpace(builder.Configuration["Telegram:BotToken"]))
    {
        builder.Services.AddTelegramPublishing(builder.Configuration);
    }

    builder.Services
        .AddControllers()
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // Panel nginx'in, nginx de Coolify'in ters vekili arkasinda calisir. Gercek istemci IP'si
    // X-Forwarded-For'dan okunmazsa hiz limiti ve istek loglari tek bir vekil adresi gorur.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Container aglarinda vekillerin adresi onceden bilinmez. Bu yuzden liste temizlenir;
        // guvenlik, API portunun disariya acilmamasindan gelir (bkz. compose.production.yaml).
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        options.ForwardLimit = 2;
    });

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddOpenApi(options => options.AddDocumentTransformer<ApiKeySecuritySchemeTransformer>());
    builder.Services.AddApiKeyAuthentication(builder.Configuration);
    builder.Services.AddApiRateLimiting();

    var app = builder.Build();

    if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        await app.Services.ApplyMigrationsAsync();
    }

    await app.SeedNewsSourcesAsync();

    app.UseForwardedHeaders();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers().RequireRateLimiting(RateLimitingExtensions.ApiPolicy);
    app.MapHealthChecks("/health").AllowAnonymous().DisableRateLimiting();

    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "Web API beklenmedik şekilde sonlandı.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Entegrasyon testlerinde WebApplicationFactory<Program> kullanabilmek icin.
public partial class Program;
