import { useMemo, useState } from 'react'
import { AppShell } from '../components/AppShell/AppShell'
import type { AppRoute } from '../router/hash'
import { getCatsThemeEnabled, setCatsThemeEnabled } from '../theme/catsTheme'

export function SettingsPage(props: { route: AppRoute }) {
  const initial = useMemo(() => getCatsThemeEnabled(), [])
  const [cats, setCats] = useState<boolean>(initial)

  return (
    <AppShell route={props.route}>
      <div className="panel app-content app-content--wip settings-panel">
        <button
          type="button"
          className={cats ? 'cats-toggle cats-toggle--on' : 'cats-toggle'}
          aria-label="cats"
          title="cats"
          onClick={() => {
            const next = !cats
            setCats(next)
            setCatsThemeEnabled(next)
          }}
        >
          cats
        </button>

        <h2>Настройки</h2>
        <p className="hint">WIP</p>
      </div>
    </AppShell>
  )
}

