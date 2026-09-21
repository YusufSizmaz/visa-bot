import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { createApi, type Api } from '../api/endpoints'

const STORAGE_KEY = 'visabot.apiKey'

interface ApiKeyContextValue {
  apiKey: string | null
  api: Api | null
  signIn: (apiKey: string, remember: boolean) => void
  signOut: () => void
}

const ApiKeyContext = createContext<ApiKeyContextValue | null>(null)

function readStoredKey(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY) ?? localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}

/**
 * API anahtarini uygulama genelinde saglar (Context + Provider deseni).
 * "Beni hatirla" secilirse localStorage, secilmezse sekme kapaninca silinen sessionStorage kullanilir.
 */
export function ApiKeyProvider({ children }: { children: ReactNode }) {
  const [apiKey, setApiKey] = useState<string | null>(readStoredKey)
  const queryClient = useQueryClient()

  const signIn = useCallback((key: string, remember: boolean) => {
    try {
      ;(remember ? localStorage : sessionStorage).setItem(STORAGE_KEY, key)
    } catch {
      // Tarayici depolamayi engelliyorsa anahtar sadece bellekte tutulur.
    }
    setApiKey(key)
  }, [])

  const signOut = useCallback(() => {
    try {
      sessionStorage.removeItem(STORAGE_KEY)
      localStorage.removeItem(STORAGE_KEY)
    } catch {
      // yok say
    }
    setApiKey(null)
    // Onceki anahtarla cekilmis veriler bir sonraki oturuma sizmasin.
    queryClient.clear()
  }, [queryClient])

  const value = useMemo<ApiKeyContextValue>(
    () => ({ apiKey, api: apiKey ? createApi(apiKey) : null, signIn, signOut }),
    [apiKey, signIn, signOut],
  )

  return <ApiKeyContext.Provider value={value}>{children}</ApiKeyContext.Provider>
}

export function useAuth() {
  const context = useContext(ApiKeyContext)

  if (!context) {
    throw new Error('useAuth, ApiKeyProvider içinde kullanılmalı.')
  }

  return context
}

/** Oturum acik sayfalarda kullanilir; anahtar yoksa hata firlatir (RequireApiKey bunu engeller). */
export function useApi(): Api {
  const { api } = useAuth()

  if (!api) {
    throw new Error('API anahtarı yok.')
  }

  return api
}
