import { useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import type { NewsSource, NewsSourceInput, SourceCategory, SourceType } from '../../api/types'
import { Button, ErrorAlert, Field, Input, Select, Textarea } from '../../components/ui'
import { useSaveSource } from './hooks'

interface FormState {
  name: string
  url: string
  type: SourceType
  fetchIntervalMinutes: string
  keywords: string
  turkishOnly: boolean
  category: SourceCategory
  itemSelector: string
  titleSelector: string
  linkSelector: string
  summarySelector: string
  publishedAtSelector: string
}

function toFormState(source: NewsSource | null): FormState {
  return {
    name: source?.name ?? '',
    url: source?.url ?? '',
    type: source?.type ?? 'Rss',
    fetchIntervalMinutes: String(source?.fetchIntervalMinutes ?? 10),
    keywords: source?.keywords?.join('\n') ?? '',
    turkishOnly: source?.turkishOnly ?? true,
    category: source?.category ?? 'Visa',
    itemSelector: source?.parsingRules?.itemSelector ?? '',
    titleSelector: source?.parsingRules?.titleSelector ?? '',
    linkSelector: source?.parsingRules?.linkSelector ?? '',
    summarySelector: source?.parsingRules?.summarySelector ?? '',
    publishedAtSelector: source?.parsingRules?.publishedAtSelector ?? '',
  }
}

/** Form durumunu API sozlesmesine cevirir. Bos alanlar null olur; dogrulamayi sunucu yapar. */
function toInput(state: FormState): NewsSourceInput {
  const keywords = state.keywords
    .split(/[\n,]/)
    .map((keyword) => keyword.trim())
    .filter(Boolean)

  const optional = (value: string) => (value.trim() === '' ? null : value.trim())

  return {
    name: state.name.trim(),
    url: state.url.trim(),
    type: state.type,
    fetchIntervalMinutes: Number(state.fetchIntervalMinutes),
    keywords: keywords.length > 0 ? keywords : null,
    turkishOnly: state.turkishOnly,
    category: state.category,
    parsingRules:
      state.type === 'Html'
        ? {
            itemSelector: state.itemSelector.trim(),
            titleSelector: state.titleSelector.trim(),
            linkSelector: optional(state.linkSelector),
            summarySelector: optional(state.summarySelector),
            publishedAtSelector: optional(state.publishedAtSelector),
          }
        : null,
  }
}

export function SourceForm({ source, onDone }: { source: NewsSource | null; onDone: () => void }) {
  const [state, setState] = useState(() => toFormState(source))
  const save = useSaveSource()

  // Sunucunun ValidationProblemDetails yanitindaki alan hatalari ("Url", "ParsingRules.ItemSelector" ...).
  const fieldErrors = save.error instanceof ApiError ? save.error.fieldErrors : {}
  const errorFor = (field: string) => fieldErrors[field]
  const hasFieldErrors = Object.keys(fieldErrors).length > 0

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setState((previous) => ({ ...previous, [key]: value }))
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    save.mutate({ id: source?.id ?? null, input: toInput(state) }, { onSuccess: onDone })
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {save.error && !hasFieldErrors && <ErrorAlert error={save.error} />}

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Ad" error={errorFor('Name')}>
          <Input required value={state.name} onChange={(event) => update('name', event.target.value)} />
        </Field>
        <Field label="Tip" error={errorFor('Type')}>
          <Select value={state.type} onChange={(event) => update('type', event.target.value as SourceType)}>
            <option value="Rss">RSS / Atom beslemesi</option>
            <option value="Html">HTML sayfası (CSS seçici)</option>
          </Select>
        </Field>
      </div>

      <Field label="Kategori" hint="Kanalda vize haberleri 🛂, uçuş kampanyaları ✈️ ile görünür." error={errorFor('Category')}>
        <Select value={state.category} onChange={(event) => update('category', event.target.value as SourceCategory)}>
          <option value="Visa">Vize ve randevu haberleri</option>
          <option value="FlightCampaign">Uçak bileti kampanyaları</option>
        </Select>
      </Field>

      <Field label="Adres" error={errorFor('Url')}>
        <Input required type="url" placeholder="https://" value={state.url} onChange={(event) => update('url', event.target.value)} />
      </Field>

      <Field label="Tarama aralığı (dakika)" hint="Resmi siteler için 5-15 dakika hem hızlı hem naziktir." error={errorFor('FetchIntervalMinutes')}>
        <Input
          required
          type="number"
          min={1}
          max={1440}
          value={state.fetchIntervalMinutes}
          onChange={(event) => update('fetchIntervalMinutes', event.target.value)}
        />
      </Field>

      <Field
        label="Anahtar kelimeler"
        hint="Her satıra bir kelime. Boş bırakılırsa kaynağın tüm haberleri kanala gider. Türkçe ekleri ve büyük/küçük harfi tanır."
        error={errorFor('Keywords')}
      >
        <Textarea rows={4} value={state.keywords} placeholder={'randevu\nvize\ncita'} onChange={(event) => update('keywords', event.target.value)} />
      </Field>

      <label className="flex items-start gap-2 rounded-xl border border-slate-200 p-3 text-sm">
        <input type="checkbox" className="mt-0.5" checked={state.turkishOnly} onChange={(event) => update('turkishOnly', event.target.checked)} />
        <span>
          <span className="font-medium text-slate-700">Sadece Türkçe haberleri kanala gönder</span>
          <span className="block text-xs text-slate-500">İngilizce, İspanyolca gibi başka dildeki haberler kaydedilir ama kanala gitmez.</span>
        </span>
      </label>

      {state.type === 'Html' && (
        <fieldset className="space-y-3 rounded-xl border border-slate-200 p-4">
          <legend className="px-1 text-sm font-medium text-slate-700">CSS seçicileri</legend>
          <p className="text-xs text-slate-500">Tarayıcıda sayfaya sağ tıklayıp "İncele" ile haber kartının etiketlerini bulabilirsin.</p>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Haber kartı *" error={errorFor('ParsingRules.ItemSelector') ?? errorFor('ParsingRules')}>
              <Input value={state.itemSelector} placeholder="article.news" onChange={(event) => update('itemSelector', event.target.value)} />
            </Field>
            <Field label="Başlık *" error={errorFor('ParsingRules.TitleSelector')}>
              <Input value={state.titleSelector} placeholder="h2" onChange={(event) => update('titleSelector', event.target.value)} />
            </Field>
            <Field label="Link">
              <Input value={state.linkSelector} placeholder="a" onChange={(event) => update('linkSelector', event.target.value)} />
            </Field>
            <Field label="Özet">
              <Input value={state.summarySelector} placeholder="p.summary" onChange={(event) => update('summarySelector', event.target.value)} />
            </Field>
            <Field label="Tarih">
              <Input value={state.publishedAtSelector} placeholder="time" onChange={(event) => update('publishedAtSelector', event.target.value)} />
            </Field>
          </div>
        </fieldset>
      )}

      <div className="flex justify-end gap-2 pt-2">
        <Button variant="secondary" onClick={onDone}>
          Vazgeç
        </Button>
        <Button type="submit" disabled={save.isPending}>
          {save.isPending ? 'Kaydediliyor…' : source ? 'Kaydet' : 'Kaynak ekle'}
        </Button>
      </div>
    </form>
  )
}
