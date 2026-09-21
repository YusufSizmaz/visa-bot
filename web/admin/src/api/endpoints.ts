import { apiBlob, apiRequest } from './client'
import type {
  ChannelMessageInput,
  ChannelMessagePage,
  ChannelMessageStatus,
  ChannelOverview,
  CursorPage,
  FetchResult,
  NewsItem,
  NewsItemFilter,
  NewsItemStats,
  NewsSource,
  NewsSourceInput,
} from './types'

/**
 * API uc noktalarinin tipli fonksiyonlari. Adres ve govde bilgisi sadece burada yasar;
 * backend'de bir yol degisirse tek dosya degisir.
 */
export function createApi(apiKey: string) {
  return {
    health: (signal?: AbortSignal) =>
      fetch('/health', { signal }).then((response) => response.ok),

    stats: (signal?: AbortSignal) =>
      apiRequest<NewsItemStats>(apiKey, '/api/news-items/stats', { signal }),

    listSources: (signal?: AbortSignal) =>
      apiRequest<NewsSource[]>(apiKey, '/api/news-sources', { signal }),

    createSource: (input: NewsSourceInput) =>
      apiRequest<{ id: string }>(apiKey, '/api/news-sources', { method: 'POST', body: input }),

    updateSource: (id: string, input: NewsSourceInput) =>
      apiRequest<void>(apiKey, `/api/news-sources/${id}`, { method: 'PUT', body: input }),

    setSourceActive: (id: string, active: boolean) =>
      apiRequest<void>(apiKey, `/api/news-sources/${id}/${active ? 'activate' : 'deactivate'}`, { method: 'POST' }),

    fetchSourceNow: (id: string) =>
      apiRequest<FetchResult>(apiKey, `/api/news-sources/${id}/fetch`, { method: 'POST' }),

    listNewsItems: (filter: NewsItemFilter, cursor: string | null, signal?: AbortSignal) =>
      apiRequest<CursorPage<NewsItem>>(apiKey, '/api/news-items', {
        signal,
        query: {
          newsSourceId: filter.newsSourceId,
          status: filter.status,
          pageSize: filter.pageSize ?? 20,
          cursor,
        },
      }),

    requeueNewsItem: (id: string) =>
      apiRequest<void>(apiKey, `/api/news-items/${id}/requeue`, { method: 'POST' }),

    channelOverview: (signal?: AbortSignal) =>
      apiRequest<ChannelOverview>(apiKey, '/api/channel', { signal }),

    listChannelMessages: (page: number, status: ChannelMessageStatus | undefined, signal?: AbortSignal) =>
      apiRequest<ChannelMessagePage>(apiKey, '/api/channel-messages', { signal, query: { page, pageSize: 15, status } }),

    createChannelMessage: (input: ChannelMessageInput) => {
      const form = new FormData()
      form.append('body', input.body)
      if (input.title.trim()) form.append('title', input.title.trim())
      if (input.linkUrl.trim()) form.append('linkUrl', input.linkUrl.trim())
      if (input.buttonText.trim()) form.append('buttonText', input.buttonText.trim())
      if (input.buttonUrl.trim()) form.append('buttonUrl', input.buttonUrl.trim())
      if (input.photo) form.append('photo', input.photo)
      if (input.scheduledAt) form.append('scheduledAt', input.scheduledAt)
      return apiRequest<{ id: string }>(apiKey, '/api/channel-messages', { method: 'POST', body: form })
    },

    changeChannelMessage: (id: string, action: 'cancel' | 'send-now' | 'retry') =>
      apiRequest<void>(apiKey, `/api/channel-messages/${id}/${action}`, { method: 'POST' }),

    channelMessagePhoto: (id: string, signal?: AbortSignal) =>
      apiBlob(apiKey, `/api/channel-messages/${id}/photo`, signal),
  }
}

export type Api = ReturnType<typeof createApi>

/**
 * TanStack Query anahtarlari tek yerde. Bir veriyi gecersiz kilmak (invalidate) icin
 * dogru anahtari hatirlamak zorunda kalmayiz.
 */
export const queryKeys = {
  health: ['health'] as const,
  stats: ['news-items', 'stats'] as const,
  sources: ['news-sources'] as const,
  newsItems: (filter: NewsItemFilter) => ['news-items', 'list', filter] as const,
  allNewsItems: ['news-items'] as const,
  channel: ['channel'] as const,
  channelMessages: ['channel-messages'] as const,
}
