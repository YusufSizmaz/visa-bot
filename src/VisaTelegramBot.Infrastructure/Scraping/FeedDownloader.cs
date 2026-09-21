using System.Buffers;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using VisaTelegramBot.Application.Abstractions.Scraping;

namespace VisaTelegramBot.Infrastructure.Scraping;

internal sealed record DownloadedContent(byte[] Body, string? CharSet, Uri FinalUri);

/// <summary>
/// Tum okuyucularin ortak indirme mantigi. HTTP ve dayaniklilik (Polly) hatalarini FeedReadException'a cevirir;
/// Application katmani HttpClient veya Polly tiplerini hic gormez.
/// </summary>
internal sealed class FeedDownloader(IHttpClientFactory httpClientFactory, IOptions<ScrapingOptions> options)
{
    public const string HttpClientName = "scraping";

    public async Task<DownloadedContent> DownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        // IHttpClientFactory: HttpClient'i her seferinde "new" ile uretmek socket tukenmesine yol acar.
        // Fabrika alttaki baglanti havuzunu yonetir.
        var client = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new FeedReadException($"Kaynak HTTP {(int)response.StatusCode} ({response.ReasonPhrase}) döndü.");
            }

            var maxBytes = options.Value.MaxResponseBytes;

            if (response.Content.Headers.ContentLength > maxBytes)
            {
                throw new FeedReadException($"Yanıt çok büyük: {response.Content.Headers.ContentLength} bayt.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var body = await ReadWithLimitAsync(stream, maxBytes, cancellationToken);

            return new DownloadedContent(
                body,
                response.Content.Headers.ContentType?.CharSet?.Trim('"'),
                response.RequestMessage?.RequestUri ?? uri);
        }
        catch (HttpRequestException exception)
        {
            throw new FeedReadException($"Kaynağa bağlanılamadı: {exception.Message}", exception);
        }
        catch (TimeoutRejectedException exception)
        {
            throw new FeedReadException("Kaynak zaman aşımına uğradı.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FeedReadException("Kaynak zaman aşımına uğradı.", exception);
        }
        catch (BrokenCircuitException exception)
        {
            throw new FeedReadException("Bu siteye art arda hata alındığı için istekler geçici olarak durduruldu (circuit breaker).", exception);
        }
    }

    private static async Task<byte[]> ReadWithLimitAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            int read;

            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (memory.Length + read > maxBytes)
                {
                    throw new FeedReadException($"Yanıt {maxBytes} bayt sınırını aştı.");
                }

                memory.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return memory.ToArray();
    }
}
