import './App.css'
import { useEffect } from 'react'
import { HomePage } from './pages/HomePage'
import { NewSessionPage } from './pages/NewSessionPage'
import { HistoryPage } from './pages/HistoryPage'
import { SettingsPage } from './pages/SettingsPage'
import { useHashRoute } from './router/hash'
import { TarifficationUiStateProvider } from './features/tariffication/state/TarifficationUiStateProvider'
import { TarifficationSessionProvider } from './features/tariffication/state/TarifficationSessionProvider'
import { applyCatsTheme, getCatsThemeEnabled } from './theme/catsTheme'

function App() {
  const route = useHashRoute()

  useEffect(() => {
    applyCatsTheme(getCatsThemeEnabled())
  }, [])

  return (
    <TarifficationUiStateProvider>
      <TarifficationSessionProvider>
        {route === '/' ? <HomePage /> : null}
        {route === '/session' ? <NewSessionPage route={route} /> : null}
        {route === '/history' ? <HistoryPage route={route} /> : null}
        {route === '/settings' ? <SettingsPage route={route} /> : null}
      </TarifficationSessionProvider>
    </TarifficationUiStateProvider>
  )
}

export default App
