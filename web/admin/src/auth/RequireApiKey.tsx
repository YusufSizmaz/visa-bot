import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from './ApiKeyProvider'

/** Korumali rota: anahtar yoksa giris sayfasina yonlendirir, girisden sonra geldigi sayfaya doner. */
export function RequireApiKey() {
  const { apiKey } = useAuth()
  const location = useLocation()

  if (!apiKey) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}
