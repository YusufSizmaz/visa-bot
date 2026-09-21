import { Link, useSearchParams } from 'react-router'
import type { ArchiveReason, DeliveryStatus, NewsItem } from '../../api/types'
import { Button, Card, deliveryStatusLabels, EmptyState, ErrorAlert, Select, Spinner, StatusBadge } from '../../components/ui'
import { formatDate, formatDateTime, formatRelative } from '../../lib/format'
import { useSources } from '../sources/hooks'
import { useNewsItems, useRequeueNewsItem } from './hooks'

const statuses: DeliveryStatus[] = ['Pending', 'Delivered', 'Failed', 'Archived']

const archiveReasonLabels: Record<ArchiveReason, string> = {
  InitialImport: 'kaynağın ilk taraması (eski haber)',
  TooOld: 'yayın tarihi çok eski',
  NotRelevant: 'anahtar kelimelerle eşleşmedi',
  NotTurkish: 'Türkçe değil',
  QueueLimit: 'tek taramadaki gönderim sınırı aşıldı',
}

export function NewsPage() {
  // Filtreler URL'de tutulur: sayfa yenilenince kaybolmaz, link olarak paylasilabilir.
  const [searchParams, setSearchParams] = useSearchParams()
  const status = (searchParams.get('status') as DeliveryStatus | null) ?? undefined
  const newsSourceId = searchParams.get('sourceId') ?? undefined

  const sources = useSources()
  const news = useNewsItems({ status, newsSourceId, pageSize: 25 })
  const items = news.data?.pages.flatMap((page) => page.items) ?? []

  function setFilter(key: 'status' | 'sourceId', value: string) {
    const next = new URLSearchParams(searchParams)
    if (value) {
      next.set(key, value)
    } else {
      next.delete(key)
    }
    setSearchParams(next, { replace: true })
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Haberler</h1>
          <p className="text-sm text-slate-500">Kaynaklardan bulunan tüm haberler, en yeniden eskiye.</p>
        </div>
        <div className="flex gap-2">
          <Select value={status ?? ''} onChange={(event) => setFilter('status', event.target.value)} className="w-40">
            <option value="">Tüm durumlar</option>
            {statuses.map((value) => (
              <option key={value} value={value}>
                {deliveryStatusLabels[value]}
              </option>
            ))}
          </Select>
          <Select value={newsSourceId ?? ''} onChange={(event) => setFilter('sourceId', event.target.value)} className="w-64">
            <option value="">Tüm kaynaklar</option>
            {sources.data?.map((source) => (
              <option key={source.id} value={source.id}>
                {source.name}
              </option>
            ))}
          </Select>
        </div>
      </div>

      <ErrorAlert error={news.error} />

      <Card>
        {news.isPending ? (
          <Spinner />
        ) : items.length === 0 ? (
          <EmptyState>Bu filtreye uyan haber yok.</EmptyState>
        ) : (
          <ul className="-my-3 divide-y divide-slate-100">
            {items.map((item) => (
              <NewsRow key={item.id} item={item} />
            ))}
          </ul>
        )}

        {news.hasNextPage && (
          <div className="mt-5 flex justify-center">
            <Button variant="secondary" onClick={() => news.fetchNextPage()} disabled={news.isFetchingNextPage}>
              {news.isFetchingNextPage ? 'Yükleniyor…' : 'Daha fazla göster'}
            </Button>
          </div>
        )}
      </Card>
    </div>
  )
}

function NewsRow({ item }: { item: NewsItem }) {
  const requeue = useRequeueNewsItem()
  const canRequeue = item.deliveryStatus === 'Failed' || item.deliveryStatus === 'Archived'

  return (
    <li className="flex flex-col gap-2 py-3 sm:flex-row sm:items-start sm:justify-between">
      <div className="min-w-0 flex-1">
        <a href={item.url} target="_blank" rel="noreferrer" className="font-medium hover:text-brand-700 hover:underline">
          {item.title}
        </a>
        {item.summary && <p className="mt-0.5 line-clamp-2 text-sm text-slate-600">{item.summary}</p>}
        <p className="mt-1 text-xs text-slate-500">
          {item.newsSourceName}
          {item.publishedAtUtc && <> · yayın {formatDate(item.publishedAtUtc)}</>}
          <span title={formatDateTime(item.discoveredAtUtc)}> · bulundu {formatRelative(item.discoveredAtUtc)}</span>
          {item.deliveredAtUtc && <span title={formatDateTime(item.deliveredAtUtc)}> · gönderildi {formatRelative(item.deliveredAtUtc)}</span>}
          {item.deliveryStatus === 'Pending' && item.deliveryAttempts > 0 && (
            <> · {item.deliveryAttempts}. deneme, sıradaki {formatRelative(item.nextDeliveryAttemptAtUtc)}</>
          )}
        </p>
        {item.archiveReason && (
          <p className={`mt-1 text-xs ${item.archiveReason === 'NotTurkish' ? 'text-amber-700' : 'text-slate-400'}`}>
            Kanala gönderilmedi: {archiveReasonLabels[item.archiveReason]}
          </p>
        )}
        {item.lastDeliveryError && <p className="mt-1 text-xs text-red-600">{item.lastDeliveryError}</p>}
        {requeue.error && <p className="mt-1 text-xs text-red-600">{requeue.error.message}</p>}
      </div>

      <div className="flex shrink-0 items-center gap-2">
        <StatusBadge status={item.deliveryStatus} />
        {item.archiveReason === 'NotTurkish' && (
          <Link
            to={`/messages?${new URLSearchParams({ linkUrl: item.url }).toString()}`}
            className="rounded-lg border border-amber-200 bg-amber-50 px-2.5 py-1 text-xs font-medium text-amber-800 hover:bg-amber-100"
            title="Haberi kendi Türkçe cümlelerinle kanala yaz"
          >
            Türkçe mesaj yaz
          </Link>
        )}
        {canRequeue && item.archiveReason !== 'NotTurkish' && (
          <Button
            size="sm"
            variant="secondary"
            disabled={requeue.isPending}
            onClick={() => requeue.mutate(item.id)}
            title="Haberi Telegram kanalına gönderilmek üzere kuyruğa al"
          >
            {requeue.isPending ? '…' : 'Kanala gönder'}
          </Button>
        )}
      </div>
    </li>
  )
}
