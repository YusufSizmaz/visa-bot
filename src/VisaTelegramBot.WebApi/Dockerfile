# Multi-stage build: SDK imaji sadece derleme icin kullanilir, son imaj yalnizca calisma zamanini icerir.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /repo

# Once sadece proje dosyalari kopyalanir ve restore edilir.
# Kod degisip paketler degismediginde Docker bu katmani onbellekten kullanir; build'ler hizlanir.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/VisaTelegramBot.Domain/VisaTelegramBot.Domain.csproj src/VisaTelegramBot.Domain/
COPY src/VisaTelegramBot.Application/VisaTelegramBot.Application.csproj src/VisaTelegramBot.Application/
COPY src/VisaTelegramBot.Infrastructure/VisaTelegramBot.Infrastructure.csproj src/VisaTelegramBot.Infrastructure/
COPY src/VisaTelegramBot.WebApi/VisaTelegramBot.WebApi.csproj src/VisaTelegramBot.WebApi/
RUN dotnet restore src/VisaTelegramBot.WebApi/VisaTelegramBot.WebApi.csproj

COPY src/ src/
RUN dotnet publish src/VisaTelegramBot.WebApi/VisaTelegramBot.WebApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Container healthcheck'i HTTP istegi atabilsin diye. aspnet imajinda ne curl ne wget bulunur;
# bu satir olmadan /health kontrolu her zaman basarisiz olur ve servis "healthy" olamaz.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Guvenlik: container root olarak degil, imajdaki yetkisiz "app" kullanicisiyla calisir.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app .
ENTRYPOINT ["dotnet", "VisaTelegramBot.WebApi.dll"]
