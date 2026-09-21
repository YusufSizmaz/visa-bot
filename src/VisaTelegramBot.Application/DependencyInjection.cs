using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VisaTelegramBot.Application.Abstractions.Scraping;
using VisaTelegramBot.Application.Common.Behaviors;

namespace VisaTelegramBot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(AssemblyReference.Assembly);

            // Siralama onemli: once loglama, sonra dogrulama. Pipeline disaridan iceriye calisir.
            config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<INewsFeedReaderResolver, NewsFeedReaderResolver>();
        return services;
    }
}
