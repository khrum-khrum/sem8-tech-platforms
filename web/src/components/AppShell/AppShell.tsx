import type { AppRoute } from '../../router/hash'
import { navigate } from '../../router/hash'

import type { ReactNode } from 'react'
import homeIconUrl from '../../assets/icons/home.png'
import telephoneIconUrl from '../../assets/icons/telephone.png'
import historyIconUrl from '../../assets/icons/history.png'
import settingsIconUrl from '../../assets/icons/settings.png'

type NavItem = { to: AppRoute; label: string; icon: ReactNode }

function IconImage(props: { src: string; size?: number }) {
  const size = props.size ?? 18
  return <img className="app-icon" src={props.src} width={size} height={size} alt="" aria-hidden="true" />
}

const navItems: NavItem[] = [
  { to: '/', label: 'Домой', icon: <IconImage src={homeIconUrl} /> },
  { to: '/session', label: 'Сессия', icon: <IconImage src={telephoneIconUrl} /> },
  { to: '/history', label: 'История', icon: <IconImage src={historyIconUrl} /> },
]

export function AppShell(props: { route: AppRoute; children: ReactNode }) {
  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header-top">
          <h1 className="app-title">CDR калькулятор</h1>
        </div>

        <div className="app-header-navrow">
          <nav className="app-nav app-nav--primary" aria-label="Разделы">
            {navItems.map((item) => {
              const active = props.route === item.to
              return (
                <button
                  key={item.to}
                  type="button"
                  className={active ? 'app-nav-button app-nav-button--active' : 'app-nav-button'}
                  onClick={() => navigate(item.to)}
                >
                  <span className="app-nav-icon">{item.icon}</span>
                  <span className="app-nav-label">{item.label}</span>
                </button>
              )
            })}
          </nav>

          <div className="app-nav app-nav--secondary">
            <button
              type="button"
              className={props.route === '/settings' ? 'app-settings app-settings--active' : 'app-settings'}
              aria-label="Настройки"
              title="Настройки"
              onClick={() => navigate('/settings')}
            >
              <IconImage src={settingsIconUrl} size={20} />
            </button>
          </div>
        </div>
      </header>

      <div className="app-surface">{props.children}</div>
    </div>
  )
}

