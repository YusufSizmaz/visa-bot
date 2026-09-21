using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;
using VisaTelegramBot.WebApi.Extensions;

namespace VisaTelegramBot.WebApi.OpenApi;

/// <summary>OpenAPI belgesine API anahtari tanimini ekler; Scalar arayuzunde anahtar girilebilir.</summary>
internal sealed class ApiKeySecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = ApiKeyAuthenticationHandler.HeaderName,
            Description = "Yönetim API anahtarı (appsettings: Security:ApiKey).",
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = ApiKeyAuthenticationHandler.SchemeName
            }
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes[ApiKeyAuthenticationHandler.SchemeName] = scheme;
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement { [scheme] = [] });

        return Task.CompletedTask;
    }
}
