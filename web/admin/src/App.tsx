import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { ApiError } from './api/client'
import { useAuth } from './auth/ApiKeyProvider'
import { LoginPage } from './auth/LoginPage'
import { RequireApiKey } from './auth/RequireApiKey'
import { Layout } from './components/Layout'
import { DashboardPage } from './features/dashboard/DashboardPage'
import { FlightsPage } from './features/flights/FlightsPage'
import { MessagesPage } from './features/messages/MessagesPage'
import { NewsPage } from './features/news/NewsPage'
import { SourcesPage } from './features/sources/SourcesPage'

export function App() {
  return (
    <BrowserRouter>
      <SignOutOnUnauthorized />
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<RequireApiKey />}>
          <Route element={<Layout />}>
            <Route index element={<DashboardPage />} />
            <Route path="messages" element={<MessagesPage />} />
            <Route path="flights" element={<FlightsPage />} />
            <Route path="sources" element={<SourcesPage />} />
            <Route path="news" element={<NewsPage />} />
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

/**
 * Herhangi bir istek 401 donerse (anahtar degismis veya iptal edilmis) oturumu kapatir.
 * Bu kontrolu her bilesende tekrar yazmak yerine sorgu cache'ini tek noktadan dinleriz.
 */
function SignOutOnUnauthorized() {
  const queryClient = useQueryClient()
  const { apiKey, signOut } = useAuth()

  useEffect(() => {
    if (!apiKey) {
      return
    }

    const isUnauthorized = (error: unknown) => error instanceof ApiError && error.isUnauthorized

    const unsubscribeQueries = queryClient.getQueryCache().subscribe((event) => {
      if (event.type === 'updated' && isUnauthorized(event.query.state.error)) {
        signOut()
      }
    })

    const unsubscribeMutations = queryClient.getMutationCache().subscribe((event) => {
      if (event.type === 'updated' && isUnauthorized(event.mutation.state.error)) {
        signOut()
      }
    })

    return () => {
      unsubscribeQueries()
      unsubscribeMutations()
    }
  }, [apiKey, queryClient, signOut])

  return null
}
