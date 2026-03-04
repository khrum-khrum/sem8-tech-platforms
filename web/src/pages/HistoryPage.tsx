import { useEffect, useMemo, useState } from 'react'
import searchIconUrl from '../assets/icons/search.png'
import { AppShell } from '../components/AppShell/AppShell'
import type { AppRoute } from '../router/hash'
import { loadCachedSessions } from '../features/history/sessionCache'
import { SessionResultsPanel } from '../features/tariffication/results/SessionResultsPanel'
import type { ResultsModel } from '../features/tariffication/results/SessionResultsPanel'
import type { PagedCallRecords, SubscriberSummary } from '../api'

function readHashQuery(): URLSearchParams {
  const raw = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash
  const q = raw.split('?')[1] ?? ''
  return new URLSearchParams(q)
}

export function HistoryPage(props: { route: AppRoute }) {
  const query = readHashQuery()
  const sid = query.get('sid')?.trim() || null

  const [searchSessionId, setSearchSessionId] = useState('')
  const [sessions, setSessions] = useState(() => loadCachedSessions())

  // local model for viewing history sessions (doesn't overwrite "current" session state)
  const [summary, setSummary] = useState<SubscriberSummary[]>([])
  const [callRecords, setCallRecords] = useState<PagedCallRecords | null>(null)
  const [phoneFilter, setPhoneFilter] = useState('')

  useEffect(() => {
    const onChange = () => {
      const q = readHashQuery()
      const newSid = q.get('sid')?.trim() || null
      setSessions(loadCachedSessions())

      // when switching session, reset local results cache
      setSummary([])
      setCallRecords(null)
      setPhoneFilter('')

      if (!newSid) {
        return
      }
    }

    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])

  const model = useMemo(
    () =>
      ({
        summary,
        setSummary,
        callRecords,
        setCallRecords,
        phoneFilter,
        setPhoneFilter,
      }) satisfies ResultsModel,
    [callRecords, phoneFilter, summary],
  )

  const filteredSessions = useMemo(() => {
    const term = searchSessionId.trim().toLowerCase()
    if (!term) return sessions
    return sessions.filter((s) => s.id.toLowerCase().includes(term))
  }, [searchSessionId, sessions])

  return (
    <AppShell route={props.route}>
      <div className="panel app-content">
        {!sid ? (
          <div className="history">
            <div className="history-search">
              <img className="history-search-icon" src={searchIconUrl} width={18} height={18} alt="" aria-hidden="true" />
              <input
                className="history-search-input"
                type="text"
                placeholder="ID сессии"
                value={searchSessionId}
                onChange={(e) => setSearchSessionId(e.target.value)}
              />
            </div>

            <div className="history-list" role="list" aria-label="Сессии">
              {filteredSessions.length === 0 ? (
                <p className="hint">Пока нет сохранённых сессий</p>
              ) : (
                filteredSessions.map((s) => (
                  <button
                    key={s.id}
                    type="button"
                    className="history-item"
                    role="listitem"
                    onClick={() => {
                      const q = new URLSearchParams()
                      q.set('sid', s.id)
                      window.location.hash = `#/history?${q.toString()}`
                    }}
                  >
                    <span className="history-item-title">Сессия</span>
                    <span className="history-item-id">{s.id}</span>
                  </button>
                ))
              )}
            </div>
          </div>
        ) : (
          <div className="history-session">
            <div className="history-session-top">
              <button type="button" className="app-nav-button" onClick={() => (window.location.hash = '#/history')}>
                ← К списку
              </button>
              <div className="history-session-meta">
                <span className="hint">Сессия</span>
                <strong>{sid}</strong>
              </div>
            </div>

            <SessionResultsPanel sessionId={sid} busy={false} model={model} />
          </div>
        )}
      </div>
    </AppShell>
  )
}

