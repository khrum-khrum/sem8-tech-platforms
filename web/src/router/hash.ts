import { useEffect, useState } from 'react'

export type AppRoute = '/' | '/session' | '/history' | '/settings'

function normalizePath(path: string): AppRoute {
  const clean = path.trim()
  switch (clean) {
    case '/':
    case '/session':
    case '/history':
    case '/settings':
      return clean
    default:
      return '/'
  }
}

export function getHashPath(): AppRoute {
  const hash = window.location.hash || ''
  const raw = hash.startsWith('#') ? hash.slice(1) : hash
  const path = raw.length === 0 ? '/' : raw.startsWith('/') ? raw : `/${raw}`
  const noQuery = path.split('?')[0]
  return normalizePath(noQuery)
}

export function navigate(to: AppRoute) {
  window.location.hash = `#${to}`
}

export function useHashRoute(): AppRoute {
  const [route, setRoute] = useState<AppRoute>(() => getHashPath())

  useEffect(() => {
    const onChange = () => setRoute(getHashPath())
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])

  return route
}

