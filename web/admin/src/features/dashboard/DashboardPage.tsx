import { Link } from 'react-router'
import { Card, EmptyState, ErrorAlert, Spinner, StatusBadge } from '../../components/ui'
import { formatDateTime, formatNumber, formatRelative } from '../../lib/format'
import { useChannelOverview } from '../messages/hooks'
import { useNewsItems, useNewsStats } from '../news/hooks'
import { useSources } from '../sources/hooks'

export function DashboardPage() {
  const overview = useChannelOverview()
  const stats = useNewsStats()
  const sources = useSources()
  const recent = useNewsItems({ status: 'Delivered', pageSize: 5 })

  const problemSources = (sources.data ?? []).filter((source) => !source.isActive || source.consecutiveFailureCount > 0)
  const recentItems = recent.data?.pages.flatMap((page) => page.items) ?? []
  const channel = overview.data?.channel

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Genel bakış</h1>
          <p className="text-sm text-slate-500">Sayılar kendiliğinden yenilenir.</p>
        </div>
        <div className="flex gap-2">
          <Link to="/messages" className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700">
            ✍️ Mesaj yaz
          </Link>
        </div>
      </div>

      <ErrorAlert error={stats.error} />

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
        <StatCard
          label="Kanal abonesi"
          value={channel?.memberCount ?? undefined}
          accent="text-brand-700"
          hint={channel ? channel.title : overview.isPending ? undefined : 'Kanal bilgisi alınamadı'}
        />
        <StatCard label="Zamanlanmış mesaj" value={overview.data?.scheduledMessages} accent="text-sky-600" to="/messages" />
        <StatCard label="Son 24 saatte giden haber" value={stats.data?.deliveredLast24Hours} to="/news?status=Delivered" />
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card title="Haber akışı">
          <dl className="grid grid-cols-2 gap-y-3 text-sm">
            <dt className="text-slate-500">Aktif kaynak</dt>
            <dd className="font-medium">
              {sources.data ? `${sources.data.filter((source) => source.isActive).length} / ${sources.data.length}` : '—'}
            </dd>
            <dt className="text-slate-500">Kuyrukta / başarısız</dt>
            <dd className="font-medium">
              <Link to="/news?status=Pending" className="hover:underline">{stats.data?.pending ?? '—'}</Link>
              {' / '}
              <Link to="/news?status=Failed" className="text-red-600 hover:underline">{stats.data?.failed ?? '—'}</Link>
            </dd>
            <dt className="text-slate-500">Son bulunan haber</dt>
            <dd className="font-medium" title={formatDateTime(stats.data?.lastDiscoveredAtUtc)}>
              {formatRelative(stats.data?.lastDiscoveredAtUtc)}
            </dd>
            <dt className="text-slate-500">Son Telegram gönderimi</dt>
            <dd className="font-medium" title={formatDateTime(stats.data?.lastDeliveredAtUtc)}>
              {formatRelative(stats.data?.lastDeliveredAtUtc)}
            </dd>
          </dl>
        </Card>

        <Card title="Dikkat isteyen kaynaklar" actions={<Link to="/sources" className="text-xs text-brand-600 hover:underline">Tümü</Link>}>
          {sources.isPending ? (
            <Spinner />
          ) : problemSources.length === 0 ? (
            <EmptyState>Tüm kaynaklar sorunsuz çalışıyor.</EmptyState>
          ) : (
            <ul className="divide-y divide-slate-100">
              {problemSources.map((source) => (
                <li key={source.id} className="py-2.5 text-sm">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium">{source.name}</span>
                    <span className={source.isActive ? 'text-amber-600' : 'text-red-600'}>
                      {source.isActive ? `${source.consecutiveFailureCount} ardışık hata` : 'Pasif'}
                    </span>
                  </div>
                  {source.lastFetchError && <p className="mt-0.5 truncate text-xs text-slate-500">{source.lastFetchError}</p>}
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      <Card title="Kanala son giden haberler" actions={<Link to="/news?status=Delivered" className="text-xs text-brand-600 hover:underline">Tümü</Link>}>
        {recent.isPending ? (
          <Spinner />
        ) : recentItems.length === 0 ? (
          <EmptyState>
            Otomatik haber henüz gönderilmedi. Sadece Türkçe ve randevu/vize ile ilgili yeni duyurular kanala gider. Yabancı dildeki önemli
            haberleri Haberler sayfasından "Türkçe mesaj yaz" ile kendin paylaşabilirsin.
          </EmptyState>
        ) : (
          <ul className="divide-y divide-slate-100">
            {recentItems.map((item) => (
              <li key={item.id} className="flex items-start justify-between gap-4 py-3">
                <div className="min-w-0">
                  <a href={item.url} target="_blank" rel="noreferrer" className="font-medium hover:text-brand-700 hover:underline">
                    {item.title}
                  </a>
                  <p className="text-xs text-slate-500">
                    {item.newsSourceName} · {formatRelative(item.deliveredAtUtc)}
                  </p>
                </div>
                <StatusBadge status={item.deliveryStatus} />
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  )
}

function StatCard({ label, value, accent, to, hint }: { label: string; value: number | undefined; accent?: string; to?: string; hint?: string }) {
  const content = (
    <div className="h-full rounded-xl border border-slate-200 bg-white p-4 shadow-xs transition-colors hover:border-slate-300">
      <p className="text-xs font-medium text-slate-500">{label}</p>
      <p className={`mt-1 text-2xl font-semibold tabular-nums ${accent ?? 'text-slate-800'}`}>{value === undefined ? '—' : formatNumber(value)}</p>
      {hint && <p className="mt-0.5 truncate text-xs text-slate-400">{hint}</p>}
    </div>
  )

  return to ? <Link to={to}>{content}</Link> : content
}
