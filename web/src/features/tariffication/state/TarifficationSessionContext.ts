import { createContext, useContext } from 'react'
import type { PagedCallRecords, ProgressEventPayload, SubscriberSummary } from '../../../api'

export type TarifficationSessionState = {
  sessionId: string | null
  startedAtMs: number | null
  finishedAtMs: number | null
  processingStatus: string
  progress: ProgressEventPayload | null
  summary: SubscriberSummary[]
  callRecords: PagedCallRecords | null
  phoneFilter: string

  setSessionId: (id: string | null) => void
  setStartedAtMs: (value: number | null) => void
  setFinishedAtMs: (value: number | null) => void
  setProcessingStatus: (value: string) => void
  setProgress: (value: ProgressEventPayload | null) => void
  setSummary: (value: SubscriberSummary[]) => void
  setCallRecords: (value: PagedCallRecords | null) => void
  setPhoneFilter: (value: string) => void

  resetSession: () => void
}

export const TarifficationSessionContext = createContext<TarifficationSessionState | null>(null)

export function useTarifficationSessionState() {
  const ctx = useContext(TarifficationSessionContext)
  if (!ctx) {
    throw new Error('useTarifficationSessionState must be used within TarifficationSessionProvider')
  }
  return ctx
}

