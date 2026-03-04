import { navigate } from '../router/hash'

type HomeLink = {
  label: string
  to: Parameters<typeof navigate>[0]
}

const links: HomeLink[] = [
  { label: 'Сессия', to: '/session' },
  { label: 'История', to: '/history' },
  { label: 'Настройки', to: '/settings' },
]

export function HomePage() {
  return (
    <main className="home">
      <header className="home-header">
        <h1>CDR калькулятор</h1>
      </header>

      <nav className="home-nav" aria-label="Разделы">
        {links.map((link) => (
          <button
            key={link.to}
            type="button"
            className="home-nav-button"
            onClick={() => navigate(link.to)}
          >
            {link.label}
          </button>
        ))}
      </nav>
    </main>
  )
}

