import { TarifficationFlow } from '../features/tariffication/TarifficationFlow'
import { AppShell } from '../components/AppShell/AppShell'
import type { AppRoute } from '../router/hash'

export function NewSessionPage(props: { route: AppRoute }) {
  return (
    <AppShell route={props.route}>
      <div className="panel app-content">
        <TarifficationFlow />
      </div>
    </AppShell>
  )
}

