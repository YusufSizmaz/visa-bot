import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { queryKeys } from '../../api/endpoints'
import type { ChannelMessageInput, ChannelMessageStatus } from '../../api/types'
import { useApi } from '../../auth/ApiKeyProvider'

export function useChannelOverview() {
  const api = useApi()

  return useQuery({
    queryKey: queryKeys.channel,
    queryFn: ({ signal }) => api.channelOverview(signal),
    refetchInterval: 30_000,
  })
}

export function useChannelMessages(page: number, status: ChannelMessageStatus | undefined) {
  const api = useApi()

  return useQuery({
    queryKey: [...queryKeys.channelMessages, { page, status }],
    queryFn: ({ signal }) => api.listChannelMessages(page, status, signal),
    // Zamanlanmis mesajlarin durumu Worker gonderdikce degisir; liste sik yenilenir.
    refetchInterval: 10_000,
    placeholderData: (previous) => previous,
  })
}

function useInvalidateMessages() {
  const queryClient = useQueryClient()

  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.channelMessages }),
      queryClient.invalidateQueries({ queryKey: queryKeys.channel }),
    ])
}

export function useCreateChannelMessage() {
  const api = useApi()
  const invalidate = useInvalidateMessages()

  return useMutation({
    mutationFn: (input: ChannelMessageInput) => api.createChannelMessage(input),
    onSuccess: invalidate,
  })
}

export function useChangeChannelMessage() {
  const api = useApi()
  const invalidate = useInvalidateMessages()

  return useMutation({
    mutationFn: ({ id, action }: { id: string; action: 'cancel' | 'send-now' | 'retry' }) => api.changeChannelMessage(id, action),
    onSuccess: invalidate,
  })
}

/** API anahtari gerektiren gorseli indirip gecici bir blob adresine cevirir; bilesen kalkinca adresi serbest birakir. */
export function useMessagePhotoUrl(id: string, enabled: boolean) {
  const api = useApi()
  const [url, setUrl] = useState<string | null>(null)

  const photo = useQuery({
    queryKey: [...queryKeys.channelMessages, 'photo', id],
    queryFn: ({ signal }) => api.channelMessagePhoto(id, signal),
    enabled,
    staleTime: Infinity,
  })

  useEffect(() => {
    if (!photo.data) {
      return
    }

    const objectUrl = URL.createObjectURL(photo.data)
    setUrl(objectUrl)

    return () => URL.revokeObjectURL(objectUrl)
  }, [photo.data])

  return url
}
