using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using VisaTelegramBot.Application.Abstractions.Caching;
using VisaTelegramBot.Application.ChannelMessages;
using VisaTelegramBot.Application.FlightDeals;
using VisaTelegramBot.Infrastructure.FlightPrices;
using VisaTelegramBot.Application.Abstractions.Locking;
using VisaTelegramBot.Application.Abstractions.Persistence;
using VisaTelegramBot.Application.Abstractions.Publishing;
using VisaTelegramBot.Application.Abstractions.Scraping;
using VisaTelegramBot.Application.NewsItems;
using VisaTelegramBot.Application.NewsSources;
using VisaTelegramBot.Infrastructure.Caching;
using VisaTelegramBot.Infrastructure.Locking;
using VisaTelegramBot.Infrastructure.Persistence;
using VisaTelegramBot.Infrastructure.Persistence.Queries;
using VisaTelegramBot.Infrastructure.Persistence.Repositories;
using VisaTelegramBot.Infrastructure.Publishing;
using VisaTelegramBot.Infrastructure.Publishing.Telegram;
using VisaTelegramBot.Infrastructure.Scraping;

namespace VisaTelegramBot.Infrastructure;

/// <summary>
/// Altyapi servislerinin DI kayitlari. Host'lar (WebApi, Worker) sadece bu metodlari cagirir;
/// hangi arayuzun hangi sinifla karsilandigini bilmek zorunda kalmazlar.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // windows-1254 gibi eski karakter setleriyle yayin yapan siteler icin.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var connectionString = configuration.GetConnectionString("Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Database tanımlı değil.");
        }

        services.AddOptions<NewsFetchingOptions>()
            .Bind(configuration.GetSection(NewsFetchingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        AddPersistence(services, connectionString);
        AddCaching(services, configuration);
        AddScraping(services, configuration);

        services.AddSingleton<IDistributedLockProvider>(new SqlServerDistributedLockProvider(connectionString));

        // Yayinci kaydi yoksa Null Object kullanilir. AddTelegramPublishing bunu degistirir.
        services.TryAddTransient<INewsPublisher, NullNewsPublisher>();
        services.TryAddTransient<IChannelInfoProvider, NullChannelInfoProvider>();

        AddFlightPrices(services, configuration);

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        return services;
    }

    public static IServiceCollection AddTelegramPublishing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Telegram istemcisine otomatik retry EKLEMIYORUZ: sendMessage idempotent degil,
        // yeniden denemek ayni haberi kanala iki kez basabilir. Yeniden deneme outbox seviyesinde, kontrollu yapilir.
        services.AddHttpClient(TelegramNewsPublisher.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .AddTypedClient<ITelegramBotClient>((httpClient, serviceProvider) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
                return new TelegramBotClient(new TelegramBotClientOptions(options.BotToken), httpClient);
            });

        services.AddTransient<TelegramNewsPublisher>();
        services.AddSingleton<NewsPublishRateLimiter>();

        services.Replace(ServiceDescriptor.Transient<INewsPublisher>(serviceProvider =>
            new RateLimitedNewsPublisher(
                serviceProvider.GetRequiredService<TelegramNewsPublisher>(),
                serviceProvider.GetRequiredService<NewsPublishRateLimiter>())));

        services.Replace(ServiceDescriptor.Transient<IChannelInfoProvider, TelegramChannelInfoProvider>());

        return services;
    }

    private static void AddPersistence(IServiceCollection services, string connectionString)
    {
        // DbContext pooling: her istekte yeni DbContext kurmak yerine havuzdan hazir nesne alinir.
        services.AddDbContextPool<AppDbContext>(options => options.UseSqlServer(
            connectionString,
            sqlServer => sqlServer.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<INewsSourceRepository, NewsSourceRepository>();
        services.AddScoped<INewsItemRepository, NewsItemRepository>();
        services.AddScoped<INewsSourceQueries, NewsSourceQueries>();
        services.AddScoped<INewsItemQueries, NewsItemQueries>();
        services.AddScoped<IChannelMessageRepository, ChannelMessageRepository>();
        services.AddScoped<IChannelMessageQueries, ChannelMessageQueries>();
        services.AddScoped<IFlightRouteRepository, FlightRouteRepository>();
        services.AddScoped<IFlightDealRepository, FlightDealRepository>();
        services.AddScoped<IFlightDealQueries, FlightDealQueries>();
    }

    private static void AddFlightPrices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FlightDealOptions>()
            .Bind(configuration.GetSection(FlightDealOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TravelpayoutsOptions>()
            .Bind(configuration.GetSection(TravelpayoutsOptions.SectionName));

        // Typed client: HttpClient, IHttpClientFactory tarafindan yonetilir ve saglayici sinifina enjekte edilir.
        // Okuma istegi idempotent oldugu icin burada standart retry + circuit breaker guvenle kullanilabilir.
        services.AddHttpClient<IFlightPriceProvider, TravelpayoutsFlightPriceProvider>(client =>
            {
                client.BaseAddress = new Uri("https://api.travelpayouts.com");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler();
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        // Redis tanimliysa HybridCache onu ikinci katman olarak otomatik kullanir; tanimli degilse sadece bellek.
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "visa-telegram-bot:";
            });
        }

        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = CachingDefaults.MaxLocalExpiration
            };
        });

        services.AddSingleton<ICacheService, HybridCacheService>();
    }

    private static void AddScraping(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ScrapingOptions>()
            .Bind(configuration.GetSection(ScrapingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var scrapingOptions = configuration.GetSection(ScrapingOptions.SectionName).Get<ScrapingOptions>() ?? new ScrapingOptions();

        services.AddHttpClient(FeedDownloader.HttpClientName, (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ScrapingOptions>>().Value;

                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Accept",
                    "application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.9, text/html;q=0.8, */*;q=0.5");

                // Zaman asimini Polly yonetir; HttpClient'in kendi zaman asimi onunla yarismasin.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                // DNS degisikliklerini gorebilmek icin baglantilar periyodik yenilenir.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxAutomaticRedirections = 5
            })
            .AddStandardResilienceHandler(options =>
            {
                // Standart pipeline: toplam zaman asimi > retry (exponential backoff + jitter) > circuit breaker > deneme zaman asimi.
                options.AttemptTimeout.Timeout = scrapingOptions.AttemptTimeout;
                options.TotalRequestTimeout.Timeout = scrapingOptions.TotalTimeout;
                options.CircuitBreaker.SamplingDuration = scrapingOptions.AttemptTimeout * 2;
                options.Retry.MaxRetryAttempts = 2;
            })
            // Her site icin ayri circuit breaker: cokmus bir site, saglikli sitelerin isteklerini durdurmaz (bulkhead).
            .SelectPipelineByAuthority();

        services.AddSingleton<FeedDownloader>();
        services.AddSingleton<INewsFeedReader, RssNewsFeedReader>();
        services.AddSingleton<INewsFeedReader, HtmlNewsFeedReader>();
    }
}
