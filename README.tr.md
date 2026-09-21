<div align="center">

# 🛂 VisaBot

**Büyükelçilik sitelerini ve havayolu kampanya sayfalarını izleyip
önemli olanı bir Telegram kanalına düşüren, kendi sunucunuzda çalışan haber hattı.**

.NET 9 üzerinde Clean Architecture, DDD ve CQRS için üretim seviyesinde bir referans olarak yazıldı.

[![Lisans: MIT](https://img.shields.io/badge/Lisans-MIT-green.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![React](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=black)](https://react.dev/)
[![Testler](https://img.shields.io/badge/testler-157%20geçiyor-success)](#testler)

[**Canlı panel**](https://visa.codynlab.dev) · [**English README**](README.md) · [**Dağıtım rehberi**](DEPLOY.md)

</div>

---

## Ne yapar?

Türkiye'den Schengen randevusu almak, günde birkaç kez aynı büyükelçilik sayfalarını yenilemek
demek. VisaBot bunu sizin yerinize yapar.

RSS/Atom beslemelerini ve HTML sayfalarını kaynak başına belirlenen aralıklarla tarar, kayıtları
anahtar kelime ve dile göre süzer, tekrarları ayıklar ve geriye kalanı bir Telegram kanalına
gönderir — yeniden deneme, hız sınırı ve tam bir iz kaydıyla birlikte. Aynı hat havayolu kampanya
sayfalarını da takip eder, böylece ucuz bilet duyuruları da aynı akışa düşer.

Her şey kendi sunucunuzda çalışır. SaaS yok, hesap yok, barındırma dışında maliyet yok.

> **Not** — [visa.codynlab.dev](https://visa.codynlab.dev) adresindeki yönetim paneli API
> anahtarıyla korunur, yani adres doğrudan bir giriş ekranı gösterir. Aşağıdaki ekran
> görüntüleri panelin içini gösteriyor.

## Ekran görüntüleri

**Kaynaklar** — her kaynağın kendi tipi, tarama aralığı, anahtar kelime filtresi ve sağlık durumu var.

![Kaynaklar](docs/screenshots/sources.png)

**Haberler** — tarayıcıların bulduğu her şey ve her kaydın neden gönderilip gönderilmediği.

![Haberler](docs/screenshots/news.png)

<div align="center">
<strong>Telegram kanalı</strong> — sonuç.<br><br>
<img src="docs/screenshots/telegram-channel.jpeg" alt="Telegram kanalı" width="330">
</div>

## Özellikler

**Toplama**

- RSS ve Atom beslemeleri; beslemesi olmayan siteler için CSS seçicili HTML kazıma
- Kaynak başına tarama aralığı, anahtar kelime filtresi ve "yalnızca Türkçe" dil filtresi
- Host başına devre kesici: çöken bir site sağlıklı olanların taramasını durdurmaz
- Eski karakter setleri (windows-1254 ve benzerleri) şeffaf biçimde ele alınır

**Gönderim**

- Outbox tarzı kuyruk: üstel geri çekilme ve ölü mektup durumu
- Telegram hız sınırı — `sendMessage` idempotent değildir, düşüncesiz bir yeniden deneme aynı
  haberi kanala iki kez basar
- Tekrarlara karşı son savunma hattı olarak içerik özeti üzerinde tekil index
- Yeni bir kaynağın ilk taraması gönderilmez, arşivlenir; böylece kaynak eklemek kanala aylarca
  geriye giden birikmiş haberi boşaltmaz

**İşletme**

- React yönetim paneli: kaynaklar, haberler, görselli ve zamanlanabilir manuel kanal mesajları
- Bir kaynağı sırası gelmeden tarama, durdurma; herhangi bir haberi elle kanala gönderme —
  filtrelerin arşivlediği kayıtlar dahil, her biri neden tutulduğu sebebiyle birlikte
- Sağlık uç noktası ve yapılandırılmış loglar; `/health` panelle aynı adresten sunulur
- Yatay ölçeklenebilir worker'lar — kuyruk `FOR UPDATE SKIP LOCKED` ile alınır

## Nasıl çalışır?

```
                 ┌──────────────┐
  RSS / Atom ───▶│              │
  HTML sayfa ───▶│   Worker     │  her kaynağı kendi aralığıyla tarar
                 │   (çekim)    │
                 └──────┬───────┘
                        │  süzme: anahtar kelime, dil, yaş
                        │  tekilleştirme: SHA-256 içerik özeti + tekil index
                        ▼
                 ┌──────────────┐
                 │  PostgreSQL  │  haberler, kuyruk, advisory lock'lar
                 └──────┬───────┘
                        │  parti alma: FOR UPDATE SKIP LOCKED
                        ▼
                 ┌──────────────┐       ┌───────────────────┐
                 │   Worker     │──────▶│ Telegram kanalı   │
                 │  (gönderim)  │       └───────────────────┘
                 └──────────────┘
                        ▲
                 ┌──────┴───────┐
                 │   Web API    │◀──── React yönetim paneli (aynı adres)
                 └──────────────┘
```

Bir haber `Bekliyor → Gönderildi` yolunu izler, denemeler tükenirse `Başarısız` olur ya da
bilinçli olarak gönderilmeden saklandıysa (ilk içe aktarım, çok eski, filtreye takıldı) doğrudan
`Arşivlendi` durumuna geçer. Sebep saklanır, böylece panel her kararı açıklayabilir.

## Mimari

Bağımlılıkları yalnızca içe bakan dört katmanlı Clean Architecture:

```
Domain          entity'ler, value object'ler, domain event'ler — hiçbir framework referansı yok
  ▲
Application     CQRS handler'ları (MediatR), doğrulama (FluentValidation), port arayüzleri
  ▲
Infrastructure  EF Core, Npgsql, Telegram, kazıma, önbellek, kilitleme — portları uygular
  ▲
Host'lar        WebApi (yönetim API'si) ve Worker (arka plan işleri)
```

**Katman kuralları gelenekle değil testle korunur.** `ArchitectureTests`, NetArchTest ile
birisi Domain'e EF Core referansı ya da Application'a bir Telegram tipi eklerse build'i kırar.

Kod tabanının asıl ilginç kısmı olduğu için birkaç kararı ayrıca belirtmek gerekir:

| Konu | Yaklaşım |
|---|---|
| Competing consumers | `FOR UPDATE SKIP LOCKED` ve `UPDATE … RETURNING` — seçme ve işaretleme tek atomik ifadede, böylece worker'lar birbirini bloklamaz |
| Dağıtık kilit | PostgreSQL advisory lock, anahtarı kaynak adının SHA-256 özeti. Redis bağımlılığı yok; çöken bir süreç bağlantısı koptuğunda kilidini bırakır |
| İyimser kilitleme | Elle tutulan bir sürüm kolonu yerine PostgreSQL'in kendi `xmin` sistem kolonu |
| Sayfalama | `OFFSET` değil, `(DiscoveredAtUtc, Id)` üzerinde keyset (cursor) sayfalama |
| Sıcak yol index'leri | Yalnızca bekleyen satırları kapsayan kısmi index'ler; tablo büyüse de kuyruk index'i küçük kalır |
| Yeniden deneme | Kazımada (idempotent) standart dayanıklılık hattı, Telegram gönderiminde (idempotent değil) bilinçli olarak **otomatik yeniden deneme yok** |
| Domain event'ler | Yalnızca başarılı commit sonrası yayınlanır; başarısız bir yan etki ana işlemi düşürmez |

Kaynaktaki yorumlar kodun ne yaptığını değil, her desenin **neden** orada olduğunu anlatır.

## Teknolojiler

| Katman | Teknoloji |
|---|---|
| Çalışma zamanı | .NET 9 |
| Mesajlaşma | MediatR (CQRS), FluentValidation |
| Veri | PostgreSQL 17, Npgsql ile EF Core 9 |
| Önbellek | HybridCache, isteğe bağlı olarak Redis destekli |
| Kazıma | AngleSharp, `Microsoft.Extensions.Http.Resilience` (Polly) |
| Telegram | Telegram.Bot |
| Loglama | Serilog |
| Arayüz | React 19, Vite, TypeScript, Tailwind CSS, TanStack Query |
| Testler | xUnit, NSubstitute, Testcontainers, NetArchTest |
| Dağıtım | Docker, nginx, Coolify |

## Başlarken

Gerekenler: [.NET 9 SDK](https://dotnet.microsoft.com/download), [Node.js 22+](https://nodejs.org/),
[Docker](https://docs.docker.com/get-docker/).

```bash
git clone https://github.com/YusufSizmaz/visa-bot.git
cd visa-bot

cp .env.example .env            # değerleri doldurun
docker compose up -d postgres redis
```

Ardından üç parçayı çalıştırın:

```bash
dotnet run --project src/VisaTelegramBot.WebApi             # http://localhost:5127
dotnet run --project src/VisaTelegramBot.Worker
npm --prefix web/admin ci && npm --prefix web/admin run dev # http://localhost:5173
```

API açılışta migration'ları uygular ve başlangıç kaynaklarını ekler; böylece panelde ilk
çalıştırmada gösterecek bir şey olur.

Tüm sistemi container'da çalıştırmak için:

```bash
docker compose --profile app up -d --build
```

### Yapılandırma

Gizli değerleri `appsettings.json` içine yazmayın. Yerelde user-secrets kullanın:

```bash
dotnet user-secrets set "Telegram:BotToken" "<@BotFather'dan>" --project src/VisaTelegramBot.Worker
dotnet user-secrets set "Telegram:ChannelId" "@kanal_adiniz"   --project src/VisaTelegramBot.Worker
```

| Ayar | Amacı |
|---|---|
| `ConnectionStrings:Database` | PostgreSQL bağlantı dizesi |
| `ConnectionStrings:Redis` | İsteğe bağlı — tanımlı değilse önbellek süreç içinde kalır |
| `Telegram:BotToken` | Bot token'ı; bot kanalda yönetici olmalı |
| `Telegram:ChannelId` | Hedef kanal, örneğin `@kanalim` |
| `Security:ApiKey` | Yönetim paneli anahtarı, en az 16 karakter |

## Testler

```bash
dotnet test
```

Dört projede 157 test: domain kuralları, application handler'ları, mimari kısıtlar ve **gerçek bir
PostgreSQL örneğine** karşı çalışan entegrasyon testleri — kilitler, tekil index'ler ve SQL
çevirileri bellek içi bir taklitle doğrulanamaz.

Entegrasyon testleri Docker çalışıyorsa Testcontainers ile tek kullanımlık bir PostgreSQL
container'ı başlatır, `VISABOT_TEST_POSTGRES` tanımlıysa sizin sunucunuzu kullanır, ikisi de
yoksa kendilerini atlar.

## Dağıtım

Depo, internete tek bir servis açan bir üretim compose yığını içerir: admin container'ı hem SPA'yı
sunar hem `/api` isteklerini backend'e aktarır, böylece tek domain ve tek sertifika her şeyi
kapsar. Veritabanı, önbellek ve API iç ağda kalır.

Coolify adımlarının tamamı, gerekli ortam değişkenleri, yedekleme ve yol boyunca karşılaştığımız
tuzaklar için: **[DEPLOY.md](DEPLOY.md)**.

## Dizin yapısı

```
src/
  VisaTelegramBot.Domain          entity'ler, value object'ler, domain event'ler
  VisaTelegramBot.Application     CQRS handler'ları, doğrulayıcılar, port arayüzleri
  VisaTelegramBot.Infrastructure  EF Core, kazıma, Telegram, önbellek, kilitleme
  VisaTelegramBot.WebApi          yönetim API'si
  VisaTelegramBot.Worker          çekim ve gönderim arka plan servisleri
tests/
  VisaTelegramBot.Domain.Tests
  VisaTelegramBot.Application.Tests
  VisaTelegramBot.ArchitectureTests
  VisaTelegramBot.IntegrationTests
web/admin                         React yönetim paneli
```

## Katkı

Issue ve pull request'ler memnuniyetle karşılanır.

- PR açmadan önce `dotnet test` ve `npm --prefix web/admin run typecheck` çalıştırın
- Katman kurallarını bozmayın — bozarsanız `ArchitectureTests` size söyler
- Yorumlarda ne yapıldığını değil, **neden** yapıldığını anlatın
- Haber kaynağı eklemek çoğunlukla hiç kod gerektirmez; panelden ya da
  `src/VisaTelegramBot.WebApi/Seed/news-sources.json` dosyasından bir kayıt yeterlidir

## Lisans

[MIT](LICENSE) — ticari kullanım dahil her amaç için serbest. Telif bildiriminin korunması
dışında hiçbir şart yok.

## Geliştiriciler

| | |
|---|---|
| **Ahmet Emre Cakmak** | [LinkedIn](https://www.linkedin.com/in/ahmet-emre-cakmak/) |
| **Yusuf Can Sızmaz** | [LinkedIn](https://www.linkedin.com/in/yusufsizmaz/) |

<div align="center">
<sub>Proje işinize yaradıysa bir ⭐ başkalarının da bulmasına yardım eder.</sub>
</div>
