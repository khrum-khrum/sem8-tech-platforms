import type { ReactNode } from 'react'
import { AppShell } from '../components/AppShell/AppShell'
import type { AppRoute } from '../router/hash'

export function WipPage(props: { route: AppRoute; title: ReactNode }) {
  return (
    <AppShell route={props.route}>
      <div className="panel app-content app-content--wip">
        <h2>{props.title}</h2>
        <p className="hint">WIP</p>
      </div>
    </AppShell>
  )
}

