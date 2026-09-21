using System.Collections.Concurrent;
using System.Reflection;
using FluentValidation;
using MediatR;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.Common.Behaviors;

/// <summary>
/// Istek handler'a ulasmadan once FluentValidation kurallarini calistirir.
/// Hata varsa handler hic calismaz, dogrudan basarisiz Result doner.
/// </summary>
internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var errors = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .Select(failure => Error.Validation(failure.PropertyName, failure.ErrorMessage))
            .Distinct()
            .ToArray();

        if (errors.Length == 0)
        {
            return await next(cancellationToken);
        }

        return ValidationFailureFactory.Create<TResponse>(new ValidationError(errors));
    }
}

/// <summary>
/// TResponse, Result ya da Result&lt;T&gt; olabilir. Generic tipi calisma zamaninda cozup dogru basarisiz sonucu uretir.
/// </summary>
internal static class ValidationFailureFactory
{
    private static readonly MethodInfo GenericFailureMethod = typeof(Result)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == nameof(Result.Failure) && method.IsGenericMethodDefinition);

    private static readonly ConcurrentDictionary<Type, MethodInfo> Cache = new();

    public static TResponse Create<TResponse>(ValidationError error)
        where TResponse : Result
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(error);
        }

        var method = Cache.GetOrAdd(
            typeof(TResponse),
            responseType => GenericFailureMethod.MakeGenericMethod(responseType.GetGenericArguments()[0]));

        return (TResponse)method.Invoke(null, [error])!;
    }
}
