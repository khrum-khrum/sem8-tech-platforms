import './App.css'
import { HomePage } from './pages/HomePage'
import { NewSessionPage } from './pages/NewSessionPage'
import { HistoryPage } from './pages/HistoryPage'
import { WipPage } from './pages/WipPage'
import { useHashRoute } from './router/hash'
import { TarifficationUiStateProvider } from './features/tariffication/state/TarifficationUiStateProvider'
import { TarifficationSessionProvider } from './features/tariffication/state/TarifficationSessionProvider'

function App() {
  const route = useHashRoute()

  return (
    <TarifficationUiStateProvider>
      <TarifficationSessionProvider>
        {route === '/' ? <HomePage /> : null}
        {route === '/session' ? <NewSessionPage route={route} /> : null}
        {route === '/history' ? <HistoryPage route={route} /> : null}
        {route === '/settings' ? <WipPage route={route} title="Настройки" /> : null}
      </TarifficationSessionProvider>
    </TarifficationUiStateProvider>
  )
}

export default App
