FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/VisaTelegramBot.Domain/VisaTelegramBot.Domain.csproj src/VisaTelegramBot.Domain/
COPY src/VisaTelegramBot.Application/VisaTelegramBot.Application.csproj src/VisaTelegramBot.Application/
COPY src/VisaTelegramBot.Infrastructure/VisaTelegramBot.Infrastructure.csproj src/VisaTelegramBot.Infrastructure/
COPY src/VisaTelegramBot.Worker/VisaTelegramBot.Worker.csproj src/VisaTelegramBot.Worker/
RUN dotnet restore src/VisaTelegramBot.Worker/VisaTelegramBot.Worker.csproj

COPY src/ src/
RUN dotnet publish src/VisaTelegramBot.Worker/VisaTelegramBot.Worker.csproj -c Release -o /app --no-restore

# Worker HTTP sunmaz; ASP.NET yerine daha kucuk olan runtime imaji yeterli.
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
USER $APP_UID

COPY --from=build /app .
ENTRYPOINT ["dotnet", "VisaTelegramBot.Worker.dll"]
