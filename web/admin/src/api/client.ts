import type { ProblemDetails } from './types'

/**
 * API'den donen hatalarin tipli hali. Bilesenler "fetch patladi" yerine
 * status, hata kodu ve alan bazli dogrulama mesajlarina ulasabilir.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string | undefined
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.title ?? problem?.detail ?? `İstek başarısız oldu (HTTP ${status}).`)
    this.name = 'ApiError'
    this.status = status
    this.code = problem?.code
    this.fieldErrors = problem?.errors ?? {}
  }

  get isUnauthorized() {
    return this.status === 401
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  query?: Record<string, string | number | boolean | undefined | null>
  signal?: AbortSignal
}

/**
 * Tum HTTP istekleri tek bir yerden gecer: API anahtari basligi, JSON donusumu ve hata cevirisi burada.
 * Bilesenler fetch'i dogrudan kullanmaz (tek sorumluluk, tekrar yok).
 */
export async function apiRequest<T>(apiKey: string, path: string, options: RequestOptions = {}): Promise<T> {
  const url = new URL(path, window.location.origin)

  for (const [key, value] of Object.entries(options.query ?? {})) {
    if (value !== undefined && value !== null && value !== '') {
      url.searchParams.set(key, String(value))
    }
  }

  const headers: Record<string, string> = { Accept: 'application/json', 'X-Api-Key': apiKey }
  const isForm = options.body instanceof FormData

  // FormData'da Content-Type'i tarayici kendisi yazar (multipart sinir degeri dahil); elle verilmez.
  if (options.body !== undefined && !isForm) {
    headers['Content-Type'] = 'application/json'
  }

  let response: Response

  try {
    response = await fetch(url, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : isForm ? (options.body as FormData) : JSON.stringify(options.body),
      signal: options.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }

    throw new ApiError(0, { title: 'API sunucusuna ulaşılamadı. Web API çalışıyor mu?' })
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

/** Gorsel gibi ikili icerik. <img src> baslik gonderemedigi icin dosya JS ile indirilip blob URL'e cevrilir. */
export async function apiBlob(apiKey: string, path: string, signal?: AbortSignal): Promise<Blob> {
  const response = await fetch(path, { headers: { 'X-Api-Key': apiKey }, signal })

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }

  return response.blob()
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}
