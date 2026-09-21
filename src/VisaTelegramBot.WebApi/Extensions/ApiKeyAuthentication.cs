using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace VisaTelegramBot.WebApi.Extensions;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Security";

    [Required(ErrorMessage = "Security:ApiKey zorunlu. Yönetim API'si anahtarsız açık bırakılamaz.")]
    [MinLength(16, ErrorMessage = "Security:ApiKey en az 16 karakter olmalı.")]
    public string ApiKey { get; init; } = string.Empty;
}

/// <summary>
/// Yonetim API'si icin basit API anahtari dogrulamasi. Istemci anahtari X-Api-Key basliginda gonderir.
/// Tek kullanicili bir yonetim paneli icin yeterlidir; cok kullanicili senaryoda JWT/OIDC tercih edilir.
/// </summary>
internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiKeyOptions> apiKeyOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var providedValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!AreEqual(providedValues.ToString(), apiKeyOptions.Value.ApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Geçersiz API anahtarı."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Sabit zamanli karsilastirma: normal string karsilastirmasi ilk farkli karakterde durur ve
    /// yanit suresinden anahtar tahmin edilebilir (timing attack). Hash'lemek uzunluk bilgisini de gizler.
    /// </summary>
    private static bool AreEqual(string provided, string expected)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));

        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}

internal static class ApiKeyAuthenticationExtensions
{
    public static IServiceCollection AddApiKeyAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiKeyOptions>()
            .Bind(configuration.GetSection(ApiKeyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, configureOptions: null);

        // Varsayilan olarak her uc nokta kimlik ister. Acik olmasi gerekenler AllowAnonymous ile isaretlenir (secure by default).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
