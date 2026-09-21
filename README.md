# VisaTelegramBot

Yurt dışı vize haberlerini ve uçak bileti kampanyalarını RSS/Atom beslemelerinden ve HTML sayfalarından
toplayıp tek bir Telegram kanalına yayınlayan .NET 9 backend'i.

Proje aynı zamanda bir öğrenme projesidir: Clean Architecture, DDD, SOLID, CQRS ve yüksek trafik desenleri
gerçek bir problem üzerinde uygulanmıştır. Kodun içindeki yorumlar her desenin **neden** orada olduğunu anlatır.

## Yığın

- **.NET 9** — Web API (yönetim paneli backend'i) + Worker (haber toplama ve gönderim)
- **PostgreSQL 17** — veri, kuyruk (`FOR UPDATE SKIP LOCKED`) ve dağıtık kilit (advisory lock)
- **Redis** — önbellek (isteğe bağlı; tanımlı değilse yalnızca bellek içi önbellek kullanılır)
- **React + Vite + Tailwind** — yönetim paneli, nginx ile sunulur ve `/api`'yi backend'e aktarır

## Yerel geliştirme

```bash
cp .env.example .env          # değerleri doldurun
docker compose up -d postgres redis

dotnet run --project src/VisaTelegramBot.WebApi      # http://localhost:5127
dotnet run --project src/VisaTelegramBot.Worker
npm --prefix web/admin ci && npm --prefix web/admin run dev   # http://localhost:5173
```

Tüm sistemi container'da çalıştırmak için:

```bash
docker compose --profile app up -d --build
```

Telegram token'ı ve API anahtarı gibi gizli değerleri `appsettings.json` yerine user-secrets'a koyun:

```bash
dotnet user-secrets set "Telegram:BotToken" "..." --project src/VisaTelegramBot.Worker
```

## Testler

```bash
dotnet test
```

Entegrasyon testleri gerçek bir PostgreSQL'e karşı çalışır. Docker çalışıyorsa Testcontainers ile
geçici bir container açılır; `VISABOT_TEST_POSTGRES` tanımlıysa o sunucu kullanılır. İkisi de yoksa
bu testler atlanır.

## Üretim

Coolify ile `visa.codynlab.dev` üzerine deploy adımları: [DEPLOY.md](DEPLOY.md).
