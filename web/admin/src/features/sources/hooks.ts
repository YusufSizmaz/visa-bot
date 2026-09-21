import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../../api/endpoints'
import type { NewsSourceInput } from '../../api/types'
import { useApi } from '../../auth/ApiKeyProvider'

// Sunucu durumu (server state) TanStack Query'de tutulur: cache, arka planda yenileme, yukleniyor/hata durumlari.
// Bilesenler useState + useEffect ile veri cekmez; bu hook'lari kullanir.

export function useSources() {
  const api = useApi()

  return useQuery({
    queryKey: queryKeys.sources,
    queryFn: ({ signal }) => api.listSources(signal),
    refetchInterval: 30_000,
  })
}

/** Kaynak degisince ilgili tum ekranlar tazelensin. */
function useInvalidateSourceData() {
  const queryClient = useQueryClient()

  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.sources }),
      queryClient.invalidateQueries({ queryKey: queryKeys.allNewsItems }),
    ])
}

export function useSaveSource() {
  const api = useApi()
  const invalidate = useInvalidateSourceData()

  return useMutation({
    mutationFn: async ({ id, input }: { id: string | null; input: NewsSourceInput }): Promise<void> => {
      if (id) {
        await api.updateSource(id, input)
      } else {
        await api.createSource(input)
      }
    },
    onSuccess: invalidate,
  })
}

export function useSetSourceActive() {
  const api = useApi()
  const invalidate = useInvalidateSourceData()

  return useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => api.setSourceActive(id, active),
    onSuccess: invalidate,
  })
}

export function useFetchSourceNow() {
  const api = useApi()
  const invalidate = useInvalidateSourceData()

  return useMutation({
    mutationFn: (id: string) => api.fetchSourceNow(id),
    // Hata olsa bile kaynagin hata sayaci degismis olabilir; listeyi her durumda tazele.
    onSettled: invalidate,
  })
}
