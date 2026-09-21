import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { ApiError } from '../api/client'
import { createApi } from '../api/endpoints'
import { Button, Input } from '../components/ui'
import { useAuth } from './ApiKeyProvider'

export function LoginPage() {
  const { apiKey, signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [key, setKey] = useState('')
  const [remember, setRemember] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [checking, setChecking] = useState(false)

  const from = (location.state as { from?: string } | null)?.from ?? '/'

  if (apiKey) {
    return <Navigate to={from} replace />
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setChecking(true)

    try {
      // Anahtari kaydetmeden once gercekten calistigini dogrula.
      await createApi(key.trim()).stats()
      signIn(key.trim(), remember)
      navigate(from, { replace: true })
    } catch (caught) {
      setError(
        caught instanceof ApiError && caught.isUnauthorized
          ? 'API anahtarı geçersiz.'
          : caught instanceof Error
            ? caught.message
            : 'Giriş yapılamadı.',
      )
    } finally {
      setChecking(false)
    }
  }

  return (
    <div className="flex min-h-full items-center justify-center p-6">
      <form onSubmit={handleSubmit} className="w-full max-w-sm rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        <div className="mb-6 text-center">
          <div className="text-4xl">🛂</div>
          <h1 className="mt-2 text-xl font-semibold">VisaBot Yönetim</h1>
          <p className="mt-1 text-sm text-slate-500">Web API'deki Security:ApiKey değerini girin.</p>
        </div>

        <label className="block text-sm font-medium text-slate-700" htmlFor="api-key">
          API anahtarı
        </label>
        <Input
          id="api-key"
          type="password"
          autoComplete="current-password"
          autoFocus
          required
          value={key}
          onChange={(event) => setKey(event.target.value)}
          className="mt-1"
        />

        <label className="mt-3 flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={remember} onChange={(event) => setRemember(event.target.checked)} />
          Bu tarayıcıda hatırla
        </label>

        {error && <p className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}

        <Button type="submit" className="mt-5 w-full" disabled={checking || key.trim().length === 0}>
          {checking ? 'Kontrol ediliyor…' : 'Giriş yap'}
        </Button>
      </form>
    </div>
  )
}
