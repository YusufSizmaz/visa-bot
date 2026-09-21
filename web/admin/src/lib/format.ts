const dateTime = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' })
const date = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium' })
const relative = new Intl.RelativeTimeFormat('tr-TR', { numeric: 'auto' })

export function formatDateTime(value: string | null | undefined) {
  return value ? dateTime.format(new Date(value)) : '—'
}

export function formatDate(value: string | null | undefined) {
  return value ? date.format(new Date(value)) : '—'
}

/** "3 dakika önce", "2 saat sonra" gibi. */
export function formatRelative(value: string | null | undefined, now = Date.now()) {
  if (!value) {
    return '—'
  }

  const seconds = Math.round((new Date(value).getTime() - now) / 1000)
  const units: Array<[Intl.RelativeTimeFormatUnit, number]> = [
    ['day', 86400],
    ['hour', 3600],
    ['minute', 60],
  ]

  for (const [unit, size] of units) {
    if (Math.abs(seconds) >= size) {
      return relative.format(Math.round(seconds / size), unit)
    }
  }

  return relative.format(seconds, 'second')
}

export function formatNumber(value: number) {
  return value.toLocaleString('tr-TR')
}
