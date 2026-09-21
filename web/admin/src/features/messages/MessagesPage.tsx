import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router'
import { ApiError } from '../../api/client'
import type { ChannelMessage, ChannelMessageStatus } from '../../api/types'
import { Badge, Button, Card, EmptyState, ErrorAlert, Field, Input, Select, Spinner, Textarea } from '../../components/ui'
import { formatDateTime, formatRelative } from '../../lib/format'
import { useChangeChannelMessage, useChannelMessages, useChannelOverview, useCreateChannelMessage, useMessagePhotoUrl } from './hooks'
import { TelegramPreview } from './TelegramPreview'

const BODY_LIMIT = 3500
const PHOTO_CAPTION_LIMIT = 900
const MAX_PHOTO_BYTES = 5 * 1024 * 1024

const emptyForm = { title: '', body: '', linkUrl: '', buttonText: '', buttonUrl: '', scheduleEnabled: false, scheduledAt: '' }

export function MessagesPage() {
  const [searchParams] = useSearchParams()
  const overview = useChannelOverview()
  const create = useCreateChannelMessage()

  // Haberler sayfasindan "Türkçe mesaj yaz" ile gelindiyse form link ve baslikla dolu acilir.
  const [form, setForm] = useState(() => ({
    ...emptyForm,
    title: searchParams.get('title') ?? '',
    linkUrl: searchParams.get('linkUrl') ?? '',
  }))
  const [photo, setPhoto] = useState<File | null>(null)
  const [photoError, setPhotoError] = useState<string | null>(null)
  const [sentNotice, setSentNotice] = useState<string | null>(null)

  const photoPreviewUrl = useMemo(() => (photo ? URL.createObjectURL(photo) : null), [photo])
  useEffect(() => () => {
    if (photoPreviewUrl) URL.revokeObjectURL(photoPreviewUrl)
  }, [photoPreviewUrl])

  const textLength = form.title.trim().length + form.body.trim().length
  const limit = photo ? PHOTO_CAPTION_LIMIT : BODY_LIMIT
  const overLimit = photo ? textLength > PHOTO_CAPTION_LIMIT : form.body.trim().length > BODY_LIMIT
  const fieldErrors = create.error instanceof ApiError ? create.error.fieldErrors : {}

  function update<K extends keyof typeof form>(key: K, value: (typeof form)[K]) {
    setForm((previous) => ({ ...previous, [key]: value }))
  }

  function handlePhoto(file: File | undefined) {
    setPhotoError(null)

    if (!file) {
      setPhoto(null)
      return
    }

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setPhotoError('Sadece JPEG, PNG veya WebP görseller kabul edilir.')
      return
    }

    if (file.size > MAX_PHOTO_BYTES) {
      setPhotoError('Görsel en fazla 5 MB olabilir.')
      return
    }

    setPhoto(file)
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSentNotice(null)

    const scheduledAt = form.scheduleEnabled && form.scheduledAt ? new Date(form.scheduledAt).toISOString() : null

    create.mutate(
      { title: form.title, body: form.body, linkUrl: form.linkUrl, buttonText: form.buttonText, buttonUrl: form.buttonUrl, photo, scheduledAt },
      {
        onSuccess: () => {
          setSentNotice(scheduledAt ? `Mesaj ${formatDateTime(scheduledAt)} için zamanlandı.` : 'Mesaj gönderim sırasına alındı; birkaç saniye içinde kanalda olur.')
          setForm(emptyForm)
          setPhoto(null)
        },
      },
    )
  }

  const channelTitle = overview.data?.channel?.title ?? 'Kanal'

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Mesaj gönder</h1>
          <p className="text-sm text-slate-500">
            {overview.data?.channel
              ? `${overview.data.channel.title}${overview.data.channel.memberCount !== null ? ` · ${overview.data.channel.memberCount.toLocaleString('tr-TR')} abone` : ''}`
              : 'Kanala özel duyuru yaz, görsel ve buton ekle, istersen ileri bir saate zamanla.'}
          </p>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_380px]">
        <Card title="Yeni mesaj">
          <form onSubmit={handleSubmit} className="space-y-4">
            {sentNotice && <p className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">{sentNotice}</p>}
            {create.error && Object.keys(fieldErrors).length === 0 && <ErrorAlert error={create.error} />}

            <Field label="Başlık" hint="Kalın yazılır. İsteğe bağlı." error={fieldErrors.Title}>
              <Input value={form.title} maxLength={200} placeholder="🇪🇸 İspanya vize randevuları açıldı" onChange={(event) => update('title', event.target.value)} />
            </Field>

            <Field label="Mesaj" error={fieldErrors.Body}>
              <Textarea
                required
                rows={7}
                value={form.body}
                placeholder="Duyurunun ayrıntılarını buraya yaz…"
                onChange={(event) => update('body', event.target.value)}
              />
              <span className={`mt-1 block text-right text-xs ${overLimit ? 'text-red-600' : 'text-slate-400'}`}>
                {photo ? `${textLength} / ${limit} (görselli mesajda başlık + metin)` : `${form.body.trim().length} / ${limit}`}
              </span>
            </Field>

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Link" hint='Mesajın altına "Detaylar" bağlantısı ekler.' error={fieldErrors.LinkUrl}>
                <Input type="url" placeholder="https://" value={form.linkUrl} onChange={(event) => update('linkUrl', event.target.value)} />
              </Field>
              <Field label="Görsel" hint="JPEG, PNG veya WebP · en fazla 5 MB" error={photoError ? [photoError] : fieldErrors.PhotoContent}>
                <div className="flex items-center gap-2">
                  <Input type="file" accept="image/jpeg,image/png,image/webp" onChange={(event) => handlePhoto(event.target.files?.[0])} key={photo ? 'has' : 'none'} />
                  {photo && (
                    <Button variant="ghost" size="sm" onClick={() => setPhoto(null)}>
                      Kaldır
                    </Button>
                  )}
                </div>
              </Field>
            </div>

            <fieldset className="grid gap-4 rounded-xl border border-slate-200 p-4 sm:grid-cols-2">
              <legend className="px-1 text-sm font-medium text-slate-700">Buton</legend>
              <Field label="Buton metni" error={fieldErrors.ButtonText}>
                <Input maxLength={40} placeholder="Randevu al" value={form.buttonText} onChange={(event) => update('buttonText', event.target.value)} />
              </Field>
              <Field label="Buton adresi" error={fieldErrors.ButtonUrl}>
                <Input type="url" placeholder="https://" value={form.buttonUrl} onChange={(event) => update('buttonUrl', event.target.value)} />
              </Field>
            </fieldset>

            <div className="rounded-xl border border-slate-200 p-4">
              <label className="flex items-center gap-2 text-sm font-medium text-slate-700">
                <input type="checkbox" checked={form.scheduleEnabled} onChange={(event) => update('scheduleEnabled', event.target.checked)} />
                İleri bir tarihte gönder
              </label>
              {form.scheduleEnabled && (
                <Input
                  type="datetime-local"
                  className="mt-3 max-w-xs"
                  required
                  value={form.scheduledAt}
                  min={toLocalInputValue(new Date())}
                  onChange={(event) => update('scheduledAt', event.target.value)}
                />
              )}
            </div>

            <div className="flex justify-end">
              <Button type="submit" disabled={create.isPending || overLimit || !form.body.trim()}>
                {create.isPending ? 'Kaydediliyor…' : form.scheduleEnabled ? 'Zamanla' : 'Kanala gönder'}
              </Button>
            </div>
          </form>
        </Card>

        <div className="space-y-2 lg:sticky lg:top-20 lg:self-start">
          <p className="text-sm font-medium text-slate-600">Telegram önizlemesi</p>
          <TelegramPreview
            channelTitle={channelTitle}
            title={form.title}
            body={form.body}
            linkUrl={form.linkUrl}
            buttonText={form.buttonText}
            photoUrl={photoPreviewUrl}
          />
        </div>
      </div>

      <MessageHistory />
    </div>
  )
}

const statusLabels: Record<ChannelMessageStatus, { label: string; className: string }> = {
  Scheduled: { label: 'Zamanlandı', className: 'bg-sky-50 text-sky-700 ring-sky-200' },
  Sent: { label: 'Gönderildi', className: 'bg-emerald-50 text-emerald-700 ring-emerald-200' },
  Failed: { label: 'Başarısız', className: 'bg-red-50 text-red-700 ring-red-200' },
  Cancelled: { label: 'İptal', className: 'bg-slate-100 text-slate-600 ring-slate-200' },
}

function MessageHistory() {
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState<ChannelMessageStatus | undefined>()
  const messages = useChannelMessages(page, status)
  const totalPages = messages.data ? Math.max(1, Math.ceil(messages.data.totalCount / messages.data.pageSize)) : 1

  return (
    <Card
      title="Gönderim geçmişi"
      actions={
        <Select
          className="w-40"
          value={status ?? ''}
          onChange={(event) => {
            setStatus((event.target.value || undefined) as ChannelMessageStatus | undefined)
            setPage(1)
          }}
        >
          <option value="">Tüm durumlar</option>
          {Object.entries(statusLabels).map(([value, style]) => (
            <option key={value} value={value}>
              {style.label}
            </option>
          ))}
        </Select>
      }
    >
      <ErrorAlert error={messages.error} />
      {messages.isPending ? (
        <Spinner />
      ) : messages.data?.items.length === 0 ? (
        <EmptyState>Henüz mesaj yok.</EmptyState>
      ) : (
        <ul className="-my-3 divide-y divide-slate-100">
          {messages.data?.items.map((message) => (
            <MessageRow key={message.id} message={message} />
          ))}
        </ul>
      )}

      {totalPages > 1 && (
        <div className="mt-4 flex items-center justify-center gap-3 text-sm">
          <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
            ← Önceki
          </Button>
          <span className="text-slate-500">
            {page} / {totalPages}
          </span>
          <Button variant="secondary" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
            Sonraki →
          </Button>
        </div>
      )}
    </Card>
  )
}

function MessageRow({ message }: { message: ChannelMessage }) {
  const change = useChangeChannelMessage()
  const photoUrl = useMessagePhotoUrl(message.id, message.hasPhoto)
  const style = statusLabels[message.status]

  return (
    <li className="flex gap-4 py-3">
      {message.hasPhoto && (
        <div className="size-16 shrink-0 overflow-hidden rounded-lg bg-slate-100">{photoUrl && <img src={photoUrl} alt="" className="size-full object-cover" />}</div>
      )}

      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <Badge className={style.className}>{style.label}</Badge>
          {message.kind === 'FlightDeal' && <Badge className="bg-violet-50 text-violet-700 ring-violet-200">Uçuş fırsatı</Badge>}
          {message.title && <span className="font-medium">{message.title}</span>}
        </div>
        <p className="mt-1 line-clamp-2 whitespace-pre-line text-sm text-slate-600">{message.body}</p>
        <p className="mt-1 text-xs text-slate-500">
          {message.status === 'Sent' && message.sentAtUtc ? (
            <span title={formatDateTime(message.sentAtUtc)}>Gönderildi {formatRelative(message.sentAtUtc)}</span>
          ) : message.status === 'Scheduled' ? (
            <span title={formatDateTime(message.scheduledAtUtc)}>Gönderim {formatRelative(message.scheduledAtUtc)}</span>
          ) : (
            <span>Oluşturuldu {formatRelative(message.createdAtUtc)}</span>
          )}
          {message.buttonText && <> · buton: {message.buttonText}</>}
          {message.attempts > 0 && message.status !== 'Sent' && <> · {message.attempts} deneme</>}
        </p>
        {message.lastError && <p className="mt-1 text-xs text-red-600">{message.lastError}</p>}
        {change.error && <p className="mt-1 text-xs text-red-600">{change.error.message}</p>}
      </div>

      <div className="flex shrink-0 items-start gap-1.5">
        {message.status === 'Scheduled' && (
          <>
            <Button size="sm" variant="secondary" disabled={change.isPending} onClick={() => change.mutate({ id: message.id, action: 'send-now' })}>
              Hemen gönder
            </Button>
            <Button size="sm" variant="danger" disabled={change.isPending} onClick={() => change.mutate({ id: message.id, action: 'cancel' })}>
              İptal
            </Button>
          </>
        )}
        {message.status === 'Failed' && (
          <Button size="sm" variant="secondary" disabled={change.isPending} onClick={() => change.mutate({ id: message.id, action: 'retry' })}>
            Tekrar dene
          </Button>
        )}
      </div>
    </li>
  )
}

function toLocalInputValue(date: Date) {
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}
