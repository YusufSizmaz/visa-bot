using System.Reflection;
using MediatR;
using NetArchTest.Rules;
using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Infrastructure.Persistence;
using VisaTelegramBot.Worker.BackgroundJobs;

namespace VisaTelegramBot.ArchitectureTests;

/// <summary>
/// Mimari testleri: Clean Architecture kurallarini kodla korur. Biri yanlislikla Domain'e EF Core
/// referansi eklerse veya Application'da Telegram tipi kullanirsa build pipeline'i kirilir.
/// </summary>
public sealed class LayerTests
{
    private const string DomainNamespace = "VisaTelegramBot.Domain";
    private const string ApplicationNamespace = "VisaTelegramBot.Application";
    private const string InfrastructureNamespace = "VisaTelegramBot.Infrastructure";
    private const string WebApiNamespace = "VisaTelegramBot.WebApi";
    private const string WorkerNamespace = "VisaTelegramBot.Worker";

    private static readonly Assembly DomainAssembly = typeof(Entity<>).Assembly;
    private static readonly Assembly ApplicationAssembly = Application.AssemblyReference.Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;
    private static readonly Assembly WorkerAssembly = typeof(FetchSchedulerOptions).Assembly;

    [Fact]
    public void Domain_DependsOnNothing()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationNamespace,
                InfrastructureNamespace,
                WebApiNamespace,
                WorkerNamespace,
                "MediatR",
                "FluentValidation",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Extensions")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrHosts()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                InfrastructureNamespace,
                WebApiNamespace,
                WorkerNamespace,
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Data.SqlClient",
                "Microsoft.AspNetCore",
                "Telegram.Bot",
                "AngleSharp",
                "Polly")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnHosts()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(WebApiNamespace, WorkerNamespace, "Microsoft.AspNetCore.Mvc")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Worker_DoesNotUseInfrastructureTypesDirectly()
    {
        // Worker sadece DI kaydi icin Infrastructure'i cagirir; is akislarini MediatR uzerinden baslatir.
        var result = Types.InAssembly(WorkerAssembly)
            .That()
            .ResideInNamespace("VisaTelegramBot.Worker.BackgroundJobs")
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, "Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Handlers_AreSealedAndInternal()
    {
        var handlerTypes = ApplicationAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.GetInterfaces().Any(IsHandlerInterface))
            .ToList();

        Assert.NotEmpty(handlerTypes);

        var violations = handlerTypes
            .Where(type => !type.IsSealed || type.IsPublic)
            .Select(type => type.FullName)
            .ToList();

        Assert.True(violations.Count == 0, "Sealed ve internal olmayan handler'lar: " + string.Join(", ", violations));
    }

    [Fact]
    public void DomainEvents_AreSealedRecordsNamedConsistently()
    {
        var result = Types.InAssembly(DomainAssembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .And()
            .AreNotAbstract()
            .Should()
            .BeSealed()
            .And()
            .HaveNameEndingWith("DomainEvent")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void AggregateProperties_HaveNoPublicSetters()
    {
        // Kapsulleme kurali: domain nesnelerinin durumu sadece davranis metodlariyla degisebilir.
        var violations = DomainAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IHasDomainEvents).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToList();

        Assert.True(violations.Count == 0, "Public setter bulunan property'ler: " + string.Join(", ", violations));
    }

    private static bool IsHandlerInterface(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();

        return definition == typeof(IRequestHandler<,>)
            || definition == typeof(IRequestHandler<>)
            || definition == typeof(INotificationHandler<>);
    }

    private static void AssertSuccessful(TestResult result)
    {
        var failing = result.FailingTypeNames ?? [];

        Assert.True(result.IsSuccessful, "Kuralı ihlal eden tipler: " + string.Join(", ", failing));
    }
}
