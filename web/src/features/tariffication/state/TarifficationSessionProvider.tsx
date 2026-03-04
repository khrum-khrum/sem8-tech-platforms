import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { TarifficationSessionContext } from './TarifficationSessionContext'
import type { TarifficationSessionState } from './TarifficationSessionContext'
import type { PagedCallRecords, ProgressEventPayload, SubscriberSummary } from '../../../api'

const storageKey = 'tp.tariffication.sessionId'

export function TarifficationSessionProvider(props: { children: ReactNode }) {
  const [sessionId, setSessionId] = useState<string | null>(() => sessionStorage.getItem(storageKey))
  const [startedAtMs, setStartedAtMs] = useState<number | null>(null)
  const [finishedAtMs, setFinishedAtMs] = useState<number | null>(null)
  const [processingStatus, setProcessingStatus] = useState('')
  const [progress, setProgress] = useState<ProgressEventPayload | null>(null)
  const [summary, setSummary] = useState<SubscriberSummary[]>([])
  const [callRecords, setCallRecords] = useState<PagedCallRecords | null>(null)
  const [phoneFilter, setPhoneFilter] = useState('')

  useEffect(() => {
    if (sessionId) sessionStorage.setItem(storageKey, sessionId)
    else sessionStorage.removeItem(storageKey)
  }, [sessionId])

  const value = useMemo<TarifficationSessionState>(
    () => ({
      sessionId,
      startedAtMs,
      finishedAtMs,
      processingStatus,
      progress,
      summary,
      callRecords,
      phoneFilter,

      setSessionId,
      setStartedAtMs,
      setFinishedAtMs,
      setProcessingStatus,
      setProgress,
      setSummary,
      setCallRecords,
      setPhoneFilter,

      resetSession: () => {
        setSessionId(null)
        setStartedAtMs(null)
        setFinishedAtMs(null)
        setProcessingStatus('')
        setProgress(null)
        setSummary([])
        setCallRecords(null)
        setPhoneFilter('')
      },
    }),
    [callRecords, finishedAtMs, phoneFilter, processingStatus, progress, sessionId, startedAtMs, summary],
  )

  return <TarifficationSessionContext.Provider value={value}>{props.children}</TarifficationSessionContext.Provider>
}

