import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ApiError } from './api/client'
import { App } from './App'
import { ApiKeyProvider } from './auth/ApiKeyProvider'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 10_000,
      // 4xx hatalari tekrar denemek anlamsiz (yanlis anahtar, bulunamadi); sadece ag ve 5xx hatalarini tekrarla.
      retry: (failureCount, error) =>
        failureCount < 2 && !(error instanceof ApiError && error.status >= 400 && error.status < 500),
      refetchOnWindowFocus: true,
    },
  },
})

const root = document.getElementById('root')

if (!root) {
  throw new Error('#root elementi bulunamadı.')
}

createRoot(root).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ApiKeyProvider>
        <App />
      </ApiKeyProvider>
    </QueryClientProvider>
  </StrictMode>,
)
