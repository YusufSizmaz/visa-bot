import { useQuery } from '@tanstack/react-query'
import { NavLink, Outlet } from 'react-router'
import { queryKeys } from '../api/endpoints'
import { useApi, useAuth } from '../auth/ApiKeyProvider'
import { Button } from './ui'

const navItems = [
  { to: '/', label: 'Genel bakış', end: true },
  { to: '/messages', label: 'Mesaj gönder', end: false },
  { to: '/flights', label: 'Uçuş fırsatları', end: false },
  { to: '/news', label: 'Haberler', end: false },
  { to: '/sources', label: 'Kaynaklar', end: false },
]

export function Layout() {
  const { signOut } = useAuth()
  const api = useApi()

  const health = useQuery({
    queryKey: queryKeys.health,
    queryFn: ({ signal }) => api.health(signal),
    refetchInterval: 15_000,
  })

  const healthy = health.data === true

  return (
    <div className="min-h-full">
      <header className="sticky top-0 z-40 border-b border-slate-200 bg-white/90 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center gap-6 px-4 py-3 sm:px-6">
          <div className="flex items-center gap-2 font-semibold">
            <span className="text-xl">🛂</span>
            <span className="hidden sm:inline">VisaBot</span>
          </div>

          <nav className="flex gap-1 overflow-x-auto">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  `whitespace-nowrap rounded-lg px-3 py-1.5 text-sm font-medium ${isActive ? 'bg-brand-50 text-brand-700' : 'text-slate-600 hover:bg-slate-100'}`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          <div className="ml-auto flex items-center gap-3">
            <span className="flex items-center gap-1.5 text-xs text-slate-500" title="Web API /health">
              <span className={`size-2 rounded-full ${health.isPending ? 'bg-slate-300' : healthy ? 'bg-emerald-500' : 'bg-red-500'}`} />
              {health.isPending ? 'Kontrol ediliyor' : healthy ? 'API sağlıklı' : 'API erişilemiyor'}
            </span>
            <Button variant="ghost" size="sm" onClick={signOut}>
              Çıkış
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6">
        <Outlet />
      </main>
    </div>
  )
}
