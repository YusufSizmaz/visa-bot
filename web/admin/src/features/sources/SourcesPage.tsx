import { useState } from 'react'
import { Link } from 'react-router'
import type { FetchResult, NewsSource } from '../../api/types'
import { Badge, Button, Card, EmptyState, ErrorAlert, Modal, Spinner } from '../../components/ui'
import { formatDateTime, formatRelative } from '../../lib/format'
import { useFetchSourceNow, useSetSourceActive, useSources } from './hooks'
import { SourceForm } from './SourceForm'

type Editing = { source: NewsSource | null } | null

export function SourcesPage() {
  const sources = useSources()
  const [editing, setEditing] = useState<Editing>(null)
  const [notice, setNotice] = useState<{ tone: 'ok' | 'error'; text: string } | null>(null)

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Kaynaklar</h1>
          <p className="text-sm text-slate-500">Worker aktif kaynakları tarama aralığına göre sürekli tarar.</p>
        </div>
        <Button onClick={() => setEditing({ source: null })}>+ Yeni kaynak</Button>
      </div>

      {notice && (
        <div
          className={`rounded-lg border px-4 py-3 text-sm ${notice.tone === 'ok' ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : 'border-red-200 bg-red-50 text-red-800'}`}
        >
          {notice.text}
          <button type="button" className="float-right text-xs opacity-60 hover:opacity-100" onClick={() => setNotice(null)}>
            kapat
          </button>
        </div>
      )}

      <ErrorAlert error={sources.error} />

      <Card>
        {sources.isPending ? (
          <Spinner />
        ) : (sources.data ?? []).length === 0 ? (
          <EmptyState>Henüz kaynak yok.</EmptyState>
        ) : (
          <div className="-m-5 overflow-x-auto">
            <table className="w-full min-w-[900px] text-left text-sm">
              <thead className="border-b border-slate-100 text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-5 py-3 font-medium">Kaynak</th>
                  <th className="px-3 py-3 font-medium">Durum</th>
                  <th className="px-3 py-3 font-medium">Filtre</th>
                  <th className="px-3 py-3 font-medium">Son tarama</th>
                  <th className="px-3 py-3 font-medium">Sıradaki</th>
                  <th className="px-5 py-3 text-right font-medium">İşlemler</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {sources.data?.map((source) => (
                  <SourceRow key={source.id} source={source} onEdit={() => setEditing({ source })} onNotice={setNotice} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {editing && (
        <Modal title={editing.source ? 'Kaynağı düzenle' : 'Yeni kaynak'} onClose={() => setEditing(null)}>
          <SourceForm source={editing.source} onDone={() => setEditing(null)} />
        </Modal>
      )}
    </div>
  )
}

function SourceRow({
  source,
  onEdit,
  onNotice,
}: {
  source: NewsSource
  onEdit: () => void
  onNotice: (notice: { tone: 'ok' | 'error'; text: string }) => void
}) {
  const fetchNow = useFetchSourceNow()
  const setActive = useSetSourceActive()

  function handleFetch() {
    fetchNow.mutate(source.id, {
      onSuccess: (result) => onNotice({ tone: 'ok', text: describeFetch(source.name, result) }),
      onError: (error) => onNotice({ tone: 'error', text: `${source.name}: ${error.message}` }),
    })
  }

  return (
    <tr className="align-top">
      <td className="px-5 py-3">
        <div className="font-medium">{source.name}</div>
        <a href={source.url} target="_blank" rel="noreferrer" className="block max-w-xs truncate text-xs text-slate-500 hover:underline">
          {source.url}
        </a>
        <div className="mt-1 flex gap-1.5">
          <Badge className={source.category === 'FlightCampaign' ? 'bg-violet-50 text-violet-700 ring-violet-200' : 'bg-sky-50 text-sky-700 ring-sky-200'}>
            {source.category === 'FlightCampaign' ? '✈️ Kampanya' : '🛂 Vize'}
          </Badge>
          <Badge className="bg-slate-50 text-slate-600 ring-slate-200">{source.type === 'Rss' ? 'RSS' : 'HTML'}</Badge>
          <Badge className="bg-slate-50 text-slate-600 ring-slate-200">{source.fetchIntervalMinutes} dk</Badge>
        </div>
      </td>
      <td className="px-3 py-3">
        {!source.isActive ? (
          <Badge className="bg-slate-100 text-slate-600 ring-slate-200">Pasif</Badge>
        ) : source.consecutiveFailureCount > 0 ? (
          <Badge className="bg-amber-50 text-amber-700 ring-amber-200">{source.consecutiveFailureCount} hata</Badge>
        ) : (
          <Badge className="bg-emerald-50 text-emerald-700 ring-emerald-200">Aktif</Badge>
        )}
        {source.lastFetchError && (
          <p className="mt-1 max-w-[220px] text-xs text-red-600" title={source.lastFetchError}>
            {source.lastFetchError}
          </p>
        )}
      </td>
      <td className="px-3 py-3 text-xs text-slate-600">
        {source.keywords?.length ? (
          <span title={source.keywords.join(', ')}>{source.keywords.length} anahtar kelime</span>
        ) : (
          <span className="text-slate-400">Yok (tüm haberler)</span>
        )}
        {source.turkishOnly && <div className="mt-1">Sadece Türkçe</div>}
      </td>
      <td className="px-3 py-3 text-xs" title={formatDateTime(source.lastFetchedAtUtc)}>
        {formatRelative(source.lastFetchedAtUtc)}
      </td>
      <td className="px-3 py-3 text-xs" title={formatDateTime(source.nextFetchAtUtc)}>
        {source.isActive ? formatRelative(source.nextFetchAtUtc) : '—'}
      </td>
      <td className="px-5 py-3">
        <div className="flex justify-end gap-1.5">
          <Button size="sm" variant="secondary" onClick={handleFetch} disabled={fetchNow.isPending || !source.isActive}>
            {fetchNow.isPending ? 'Taranıyor…' : 'Şimdi tara'}
          </Button>
          <Link to={`/news?sourceId=${source.id}`} className="rounded-lg px-2.5 py-1 text-xs font-medium text-slate-600 hover:bg-slate-100">
            Haberler
          </Link>
          <Button size="sm" variant="ghost" onClick={onEdit}>
            Düzenle
          </Button>
          <Button
            size="sm"
            variant={source.isActive ? 'danger' : 'secondary'}
            disabled={setActive.isPending}
            onClick={() => setActive.mutate({ id: source.id, active: !source.isActive })}
          >
            {source.isActive ? 'Durdur' : 'Başlat'}
          </Button>
        </div>
      </td>
    </tr>
  )
}

function describeFetch(name: string, result: FetchResult) {
  if (result.status === 'Skipped') {
    return `${name}: tarama atlandı (${result.skipReason ?? 'bilinmeyen neden'}).`
  }

  return `${name}: ${result.entriesRead} kayıt okundu, ${result.newItems} yeni haber, ${result.queuedForDelivery} tanesi kanala gönderilecek, ${result.archived} arşivlendi.`
}
