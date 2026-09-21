import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../../api/endpoints'
import type { NewsItemFilter } from '../../api/types'
import { useApi } from '../../auth/ApiKeyProvider'

/**
 * Cursor sayfalama backend'deki keyset pagination ile birebir eslesir:
 * her sayfa bir sonrakinin imlecini (nextCursor) verir, "Daha fazla" butonu onu gonderir.
 */
export function useNewsItems(filter: NewsItemFilter) {
  const api = useApi()

  return useInfiniteQuery({
    queryKey: queryKeys.newsItems(filter),
    queryFn: ({ pageParam, signal }) => api.listNewsItems(filter, pageParam, signal),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor,
    refetchInterval: 30_000,
  })
}

export function useNewsStats() {
  const api = useApi()

  return useQuery({
    queryKey: queryKeys.stats,
    queryFn: ({ signal }) => api.stats(signal),
    refetchInterval: 15_000,
  })
}

export function useRequeueNewsItem() {
  const api = useApi()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => api.requeueNewsItem(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.allNewsItems }),
  })
}
