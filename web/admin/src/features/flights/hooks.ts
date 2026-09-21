import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../../api/endpoints'
import type { FlightDealStatus, FlightRouteInput } from '../../api/types'
import { useApi } from '../../auth/ApiKeyProvider'

export function useFlightRoutes() {
  const api = useApi()

  return useQuery({ queryKey: queryKeys.flightRoutes, queryFn: ({ signal }) => api.listFlightRoutes(signal), refetchInterval: 30_000 })
}

export function useFlightDeals(status: FlightDealStatus | undefined) {
  const api = useApi()

  return useQuery({
    queryKey: [...queryKeys.flightDeals, { status }],
    queryFn: ({ signal }) => api.listFlightDeals(status, signal),
    refetchInterval: 30_000,
  })
}

function useInvalidateFlights() {
  const queryClient = useQueryClient()

  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.flightRoutes }),
      queryClient.invalidateQueries({ queryKey: queryKeys.flightDeals }),
      queryClient.invalidateQueries({ queryKey: queryKeys.channelMessages }),
      queryClient.invalidateQueries({ queryKey: queryKeys.channel }),
    ])
}

export function useSaveFlightRoute() {
  const api = useApi()
  const invalidate = useInvalidateFlights()

  return useMutation({
    mutationFn: ({ id, input }: { id: string | null; input: FlightRouteInput }) => api.saveFlightRoute(id, input),
    onSuccess: invalidate,
  })
}

export function useSetFlightRouteActive() {
  const api = useApi()
  const invalidate = useInvalidateFlights()

  return useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => api.setFlightRouteActive(id, active),
    onSuccess: invalidate,
  })
}

export function useCheckFlightRoute() {
  const api = useApi()
  const invalidate = useInvalidateFlights()

  return useMutation({ mutationFn: (id: string) => api.checkFlightRoute(id), onSettled: invalidate })
}

export function useFlightDealAction() {
  const api = useApi()
  const invalidate = useInvalidateFlights()

  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: 'publish' | 'dismiss' }) =>
      action === 'publish' ? api.publishFlightDeal(id).then(() => undefined) : api.dismissFlightDeal(id),
    onSuccess: invalidate,
  })
}
