# Üretime alma (Coolify)

Hedef: `https://visa.codynlab.dev` üzerinden yönetim paneli ve API, arka planda haber toplayan Worker.

Yığın tek bir `compose.production.yaml` dosyasıyla gelir:

| Servis | Görevi | Dışarı açık mı? |
|---|---|---|
| `admin` | Paneli sunar, `/api` isteklerini API'ye aktarır (nginx) | **Evet** — domain buraya bağlanır |
| `api` | Yönetim API'si, migration'ları uygular, kaynakları seed eder | Hayır |
| `worker` | Haberleri çeker ve Telegram'a gönderir | Hayır |
| `postgres` | Veritabanı | Hayır |
| `redis` | Önbellek | Hayır |

Panel ve API aynı adresten sunulduğu için CORS ayarı gerekmez ve tek bir sertifika yeterlidir.

---

## 1. DNS

`visa.codynlab.dev` → `116.203.22.145` (A kaydı). Sertifikayı Coolify Let's Encrypt ile kendisi alır,
bu yüzden kayıt yayına girmeden deploy etmeyin.

Kontrol:

```bash
dig +short visa.codynlab.dev
```

## 2. Coolify'da kaynak oluşturma

1. **+ New → Docker Compose** (Dockerfile veya Nixpacks değil).
2. Kaynak: `https://github.com/YusufSizmaz/visa-bot`, dal `main`.
3. **Compose dosyası:** `compose.production.yaml`
4. **Domain:** `admin` servisine `https://visa.codynlab.dev` yazın. Diğer servislere domain vermeyin.

## 3. Ortam değişkenleri

Coolify → Environment Variables. Dördü zorunludur; biri eksikse deploy **anlaşılır bir hatayla durur**
(sessizce zayıf bir parolayla ayağa kalkmaz).

| Değişken | Açıklama |
|---|---|
| `POSTGRES_PASSWORD` | Veritabanı parolası. Uzun ve rastgele üretin. |
| `API_KEY` | Panele giriş anahtarı, **en az 16 karakter**. |
| `TELEGRAM_BOT_TOKEN` | BotFather'dan aldığınız token. |
| `TELEGRAM_CHANNEL_ID` | Botun yönetici olduğu kanal, örn. `@VisaBotCodynlab`. |

İsteğe bağlı: `POSTGRES_DB` (varsayılan `VisaTelegramBot`), `POSTGRES_USER` (varsayılan `visabot`).

Değer üretmek için:

```bash
openssl rand -base64 32
```

> Bu değerleri koda veya `.env` dosyasına yazmayın; `.env` git'e gönderilmez ve sunucuda kullanılmaz.

## 4. Deploy

**Deploy**'a basın. İlk kurulumda sıra şudur:

1. `postgres` ve `redis` ayağa kalkar, sağlık kontrolünü geçer.
2. `api` başlar, **migration'ları uygular** ve `Seed/news-sources.json` içindeki 9 kaynağı ekler.
3. `api` sağlıklı olunca `worker` ve `admin` başlar.

`worker`, `api` sağlıklı olmadan başlamaz; böylece tablolar oluşmadan haber çekmeye çalışmaz.

## 5. Deploy sonrası kontrol

```bash
curl -s https://visa.codynlab.dev/health          # "Healthy" dönmeli
curl -s -o /dev/null -w '%{http_code}\n' \
     https://visa.codynlab.dev/api/news-sources   # anahtarsız 401 dönmeli
```

Panele `https://visa.codynlab.dev` adresinden girip `API_KEY` değerini yazın. Sağ üstteki
gösterge "API sağlıklı" demeli.

Coolify loglarında `worker` servisinde şuna benzer satırlar görmelisiniz:

```
Kaynak Ucuza Uçak - Ucuz Uçak Bileti İlanları çekildi: 10 kayıt okundu, 10 yeni, N kuyrukta
```

**İlk çalıştırmada çoğu kayıt "arşivlendi" olarak geçer** — `NewsFetching:MaxItemAge` (3 gün) filtresi
geçmiş haberleri kuyruğa almaz. Bu kasıtlıdır: bot açıldığı anda kanala yüzlerce eski haber düşmez.
Kanala gönderim, kaynaklarda yeni içerik çıktıkça başlar.

## 6. Yedekleme

Veri tek bir yerde: `postgres-data` volume'u. Coolify'ın **Backups** sekmesinden bu kaynağa
zamanlanmış yedek tanımlayın (günlük yeterli). Elle yedek:

```bash
docker exec <postgres-container> pg_dump -U visabot VisaTelegramBot | gzip > visabot-$(date +%F).sql.gz
```

Geri yükleme:

```bash
gunzip -c visabot-2026-09-21.sql.gz | docker exec -i <postgres-container> psql -U visabot -d VisaTelegramBot
```

## 7. Güncelleme

`main` dalına push edin, Coolify'da **Redeploy**. Migration'lar açılışta otomatik uygulanır.
Panel nginx'i API adresini her istekte yeniden çözümler, bu yüzden API yeniden dağıtılınca
panel kendini toparlar; elle yeniden başlatma gerekmez.

---

## Bilinmesi gerekenler

- **Kaynak seed'i tek yönlüdür.** `api` her açılışta `Seed/news-sources.json` dosyasını okur, aynı
  adresle kayıt varsa **atlar**. Panelden yaptığınız değişiklikler deploy'da ezilmez; buna karşılık
  seed dosyasındaki bir düzeltme de mevcut kayda yansımaz. Mevcut bir kaynağı değiştirmek için paneli
  kullanın.
- **Worker'ı ölçeklemek güvenlidir.** Kuyruk `FOR UPDATE SKIP LOCKED` ile okunur ve kaynak çekimi
  PostgreSQL advisory lock ile korunur; iki kopya aynı haberi iki kez göndermez.
  Coolify'da `worker` için replica sayısını artırabilirsiniz.
- **Migration'ları yalnızca `api` uygular** (`Database__ApplyMigrationsOnStartup`). Worker'a bu
  değişkeni vermeyin.
- **API ve veritabanı dışarı kapalıdır.** Bu tasarımın bir parçası: API, `X-Forwarded-For` başlığına
  koşulsuz güvenir (container ağında vekil adresi önceden bilinemez). `api` portunu `ports:` ile
  dışarı açarsanız hız limiti atlatılabilir hale gelir.
- **DataProtection anahtarları container içinde tutulur.** Uygulama çerez/oturum kullanmadığı
  (kimlik doğrulama `X-Api-Key` başlığıyla) için bu zararsızdır; açılış logundaki uyarı beklenen
  davranıştır.
