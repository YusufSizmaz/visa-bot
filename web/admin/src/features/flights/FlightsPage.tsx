import { useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import type { FlightDeal, FlightDealStatus, FlightRoute } from '../../api/types'
import { Badge, Button, Card, EmptyState, ErrorAlert, Field, Input, Modal, Select, Spinner } from '../../components/ui'
import { formatDateTime, formatRelative } from '../../lib/format'
import { useCheckFlightRoute, useFlightDealAction, useFlightDeals, useFlightRoutes, useSaveFlightRoute, useSetFlightRouteActive } from './hooks'

const departureFormat = new Intl.DateTimeFormat('tr-TR', { weekday: 'short', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' })

function formatPrice(price: number, currency: string) {
  const symbol = currency === 'TRY' ? '₺' : currency === 'EUR' ? '€' : currency === 'USD' ? '$' : currency
  return `${price.toLocaleString('tr-TR', { maximumFractionDigits: 0 })} ${symbol}`
}

export function FlightsPage() {
  const routes = useFlightRoutes()
  const [editing, setEditing] = useState<{ route: FlightRoute | null } | null>(null)
  const [dealStatus, setDealStatus] = useState<FlightDealStatus | undefined>('New')
  const deals = useFlightDeals(dealStatus)

  const tokenMissing = routes.data?.some((route) => route.lastError?.includes('Travelpayouts:ApiToken'))

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Uçuş fırsatları</h1>
          <p className="text-sm text-slate-500">Takip ettiğin rotalarda belirlediğin fiyatın altında bilet çıkınca burada listelenir.</p>
        </div>
        <Button onClick={() => setEditing({ route: null })}>+ Rota ekle</Button>
      </div>

      {tokenMissing && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          Travelpayouts API token'ı tanımlı değil. travelpayouts.com'a ücretsiz kayıt olup token'ı al ve şu komutla gir:
          <code className="mt-2 block rounded bg-white/70 px-2 py-1 font-mono text-xs">
            dotnet user-secrets set "Travelpayouts:ApiToken" "TOKEN" --project src\VisaTelegramBot.Worker
          </code>
        </div>
      )}

      <Card title="Takip edilen rotalar">
        <ErrorAlert error={routes.error} />
        {routes.isPending ? (
          <Spinner />
        ) : routes.data?.length === 0 ? (
          <EmptyState>Henüz rota yok. Örneğin İstanbul (IST) → Madrid (MAD) ekleyebilirsin.</EmptyState>
        ) : (
          <ul className="-my-3 divide-y divide-slate-100">
            {routes.data?.map((route) => (
              <RouteRow key={route.id} route={route} onEdit={() => setEditing({ route })} />
            ))}
          </ul>
        )}
      </Card>

      <Card
        title="Bulunan fırsatlar"
        actions={
          <Select className="w-44" value={dealStatus ?? ''} onChange={(event) => setDealStatus((event.target.value || undefined) as FlightDealStatus | undefined)}>
            <option value="New">Karar bekleyenler</option>
            <option value="Published">Kanala gönderilenler</option>
            <option value="Dismissed">Reddedilenler</option>
            <option value="">Tümü</option>
          </Select>
        }
      >
        <ErrorAlert error={deals.error} />
        {deals.isPending ? (
          <Spinner />
        ) : deals.data?.length === 0 ? (
          <EmptyState>Bu filtrede fırsat yok.</EmptyState>
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            {deals.data?.map((deal) => (
              <DealCard key={deal.id} deal={deal} />
            ))}
          </div>
        )}
      </Card>

      {editing && (
        <Modal title={editing.route ? 'Rotayı düzenle' : 'Yeni rota'} onClose={() => setEditing(null)}>
          <RouteForm route={editing.route} onDone={() => setEditing(null)} />
        </Modal>
      )}
    </div>
  )
}

function RouteRow({ route, onEdit }: { route: FlightRoute; onEdit: () => void }) {
  const check = useCheckFlightRoute()
  const setActive = useSetFlightRouteActive()

  return (
    <li className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{route.label}</span>
          <Badge className="bg-slate-50 font-mono text-slate-600 ring-slate-200">
            {route.origin} → {route.destination}
          </Badge>
          {!route.isActive && <Badge className="bg-slate-100 text-slate-600 ring-slate-200">Durduruldu</Badge>}
          {route.autoPublish && <Badge className="bg-violet-50 text-violet-700 ring-violet-200">Otomatik yayın</Badge>}
        </div>
        <p className="mt-1 text-xs text-slate-500">
          En fazla {formatPrice(route.maxPrice, 'TRY')} · önümüzdeki {route.monthsAhead} ay · her {Math.round(route.checkIntervalMinutes / 60)} saatte bir
          {route.lastCheckedAtUtc && <span title={formatDateTime(route.lastCheckedAtUtc)}> · son kontrol {formatRelative(route.lastCheckedAtUtc)}</span>}
        </p>
        {route.lastError && <p className="mt-1 text-xs text-red-600">{route.lastError}</p>}
        {check.data && (
          <p className="mt-1 text-xs text-emerald-700">
            {check.data.skipped ? 'Kontrol atlandı.' : `${check.data.offersRead} teklif incelendi, ${check.data.newDeals} yeni fırsat bulundu.`}
          </p>
        )}
        {check.error && !route.lastError && <p className="mt-1 text-xs text-red-600">{check.error.message}</p>}
      </div>

      <div className="flex shrink-0 gap-1.5">
        <Button size="sm" variant="secondary" disabled={check.isPending || !route.isActive} onClick={() => check.mutate(route.id)}>
          {check.isPending ? 'Kontrol ediliyor…' : 'Şimdi kontrol et'}
        </Button>
        <Button size="sm" variant="ghost" onClick={onEdit}>
          Düzenle
        </Button>
        <Button
          size="sm"
          variant={route.isActive ? 'danger' : 'secondary'}
          disabled={setActive.isPending}
          onClick={() => setActive.mutate({ id: route.id, active: !route.isActive })}
        >
          {route.isActive ? 'Durdur' : 'Başlat'}
        </Button>
      </div>
    </li>
  )
}

function DealCard({ deal }: { deal: FlightDeal }) {
  const action = useFlightDealAction()

  return (
    <div className="rounded-xl border border-slate-200 p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-xs text-slate-500">{deal.routeLabel}</p>
          <p className="text-2xl font-semibold tabular-nums text-brand-700">{formatPrice(deal.price, deal.currency)}</p>
        </div>
        {deal.status === 'Published' && <Badge className="bg-emerald-50 text-emerald-700 ring-emerald-200">Gönderildi</Badge>}
        {deal.status === 'Dismissed' && <Badge className="bg-slate-100 text-slate-600 ring-slate-200">Reddedildi</Badge>}
      </div>

      <dl className="mt-2 space-y-0.5 text-sm text-slate-600">
        <div>📅 {departureFormat.format(new Date(deal.departureAt))}</div>
        <div>🔁 {deal.transfers === 0 ? 'Direkt' : `${deal.transfers} aktarma`}</div>
        {deal.airline && (
          <div>
            🛫 {deal.airline} {deal.flightNumber}
          </div>
        )}
      </dl>

      <p className="mt-2 text-xs text-slate-400">Bulundu {formatRelative(deal.foundAtUtc)}</p>
      {action.error && <p className="mt-1 text-xs text-red-600">{action.error.message}</p>}

      <div className="mt-3 flex flex-wrap gap-2">
        <a href={deal.bookingUrl} target="_blank" rel="noreferrer" className="rounded-lg px-2.5 py-1 text-xs font-medium text-brand-700 hover:bg-brand-50">
          Bileti aç ↗
        </a>
        {deal.status === 'New' && (
          <>
            <Button size="sm" disabled={action.isPending} onClick={() => action.mutate({ id: deal.id, action: 'publish' })}>
              Kanala gönder
            </Button>
            <Button size="sm" variant="ghost" disabled={action.isPending} onClick={() => action.mutate({ id: deal.id, action: 'dismiss' })}>
              Reddet
            </Button>
          </>
        )}
      </div>
    </div>
  )
}

function RouteForm({ route, onDone }: { route: FlightRoute | null; onDone: () => void }) {
  const save = useSaveFlightRoute()
  const [state, setState] = useState({
    origin: route?.origin ?? 'IST',
    destination: route?.destination ?? '',
    label: route?.label ?? '',
    maxPrice: String(route?.maxPrice ?? 3000),
    monthsAhead: String(route?.monthsAhead ?? 3),
    checkIntervalHours: String(route ? Math.round(route.checkIntervalMinutes / 60) : 6),
    autoPublish: route?.autoPublish ?? false,
  })

  const fieldErrors = save.error instanceof ApiError ? save.error.fieldErrors : {}

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    save.mutate(
      {
        id: route?.id ?? null,
        input: {
          origin: state.origin.trim().toUpperCase(),
          destination: state.destination.trim().toUpperCase(),
          label: state.label.trim() || null,
          maxPrice: Number(state.maxPrice),
          monthsAhead: Number(state.monthsAhead),
          checkIntervalMinutes: Number(state.checkIntervalHours) * 60,
          autoPublish: state.autoPublish,
        },
      },
      { onSuccess: onDone },
    )
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {save.error && Object.keys(fieldErrors).length === 0 && <ErrorAlert error={save.error} />}

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Kalkış (IATA kodu)" hint="IST, SAW, ESB, ADB…" error={fieldErrors.Origin}>
          <Input required maxLength={3} value={state.origin} onChange={(event) => setState({ ...state, origin: event.target.value })} className="font-mono uppercase" />
        </Field>
        <Field label="Varış (IATA kodu)" hint="MAD, BCN, ROM, PAR…" error={fieldErrors.Destination}>
          <Input required maxLength={3} value={state.destination} onChange={(event) => setState({ ...state, destination: event.target.value })} className="font-mono uppercase" />
        </Field>
      </div>

      <Field label="Rota adı" hint='Kanal mesajında görünür. Boş bırakılırsa "IST → MAD" yazılır.' error={fieldErrors.Label}>
        <Input maxLength={100} placeholder="İstanbul → Madrid" value={state.label} onChange={(event) => setState({ ...state, label: event.target.value })} />
      </Field>

      <div className="grid gap-4 sm:grid-cols-3">
        <Field label="Azami fiyat (₺)" error={fieldErrors.MaxPrice}>
          <Input required type="number" min={1} value={state.maxPrice} onChange={(event) => setState({ ...state, maxPrice: event.target.value })} />
        </Field>
        <Field label="Kaç ay ileri" error={fieldErrors.MonthsAhead}>
          <Input required type="number" min={1} max={12} value={state.monthsAhead} onChange={(event) => setState({ ...state, monthsAhead: event.target.value })} />
        </Field>
        <Field label="Kontrol (saatte bir)" error={fieldErrors.CheckIntervalMinutes}>
          <Input required type="number" min={1} max={24} value={state.checkIntervalHours} onChange={(event) => setState({ ...state, checkIntervalHours: event.target.value })} />
        </Field>
      </div>

      <label className="flex items-start gap-2 rounded-xl border border-slate-200 p-3 text-sm">
        <input type="checkbox" className="mt-0.5" checked={state.autoPublish} onChange={(event) => setState({ ...state, autoPublish: event.target.checked })} />
        <span>
          <span className="font-medium text-slate-700">Bulunan fırsatları onay beklemeden kanala gönder</span>
          <span className="block text-xs text-slate-500">Kapalıysa fırsatlar burada bekler; sen "Kanala gönder" dersin. Az mesaj istiyorsan kapalı bırak.</span>
        </span>
      </label>

      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onDone}>
          Vazgeç
        </Button>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? 'Kaydediliyor…' : 'Kaydet'}
        </Button>
      </div>
    </form>
  )
}
