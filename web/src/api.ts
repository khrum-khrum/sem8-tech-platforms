import { API_BASE_URL } from './config'

export type UploadKind = 'cdr' | 'tariff' | 'subscribers'

interface ApiErrorBody {
  title?: string
  detail?: string
  message?: string
}

export interface UploadResult {
  recordsImported: number
  message: string
}

export interface ProgressEventPayload {
  processed: number
  total: number
  status: string
  error?: string | null
  percent?: number
}

export interface SubscriberSummary {
  phoneNumber: string
  clientName: string
  callCount: number
  totalBillableSec: number
  totalCharge: number
}

export interface AppliedTariff {
  id: number
  prefix: string
  destination: string
  ratePerMin: number
  connectionFee: number
}

export interface CallRecordDetail {
  id: number
  startTime: string
  endTime: string
  callingParty: string
  calledParty: string
  direction: string
  disposition: string
  durationSec: number
  billableSec: number
  originalCharge?: number | null
  computedCharge?: number | null
  accountCode?: string | null
  callId: string
  trunkName?: string | null
  appliedTariff?: AppliedTariff | null
}

export interface PagedCallRecords {
  items: CallRecordDetail[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

const UPLOAD_ENDPOINTS: Record<UploadKind, string> = {
  cdr: '/upload/cdr',
  tariff: '/upload/tariff',
  subscribers: '/upload/subscribers',
}

async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, init)
  if (response.ok) {
    return (await response.json()) as T
  }

  let errorMessage = `HTTP ${response.status}`
  try {
    const errorBody = (await response.json()) as ApiErrorBody
    errorMessage = errorBody.detail ?? errorBody.message ?? errorBody.title ?? errorMessage
  } catch {
    // ignore non-json error responses
  }

  throw new Error(errorMessage)
}

export async function createSession(): Promise<string> {
  const payload = await apiFetch<{ sessionId: string }>('/api/sessions', {
    method: 'POST',
  })
  return payload.sessionId
}

export async function uploadFile(
  sessionId: string,
  kind: UploadKind,
  file: File,
): Promise<UploadResult> {
  const formData = new FormData()
  formData.append('file', file)

  return apiFetch<UploadResult>(`/api/sessions/${sessionId}${UPLOAD_ENDPOINTS[kind]}`, {
    method: 'POST',
    body: formData,
  })
}

export async function runTariffication(sessionId: string): Promise<void> {
  await apiFetch<{ message: string; sessionId: string }>(`/api/sessions/${sessionId}/run`, {
    method: 'POST',
  })
}

export async function getSummary(sessionId: string): Promise<SubscriberSummary[]> {
  return apiFetch<SubscriberSummary[]>(`/api/sessions/${sessionId}/results/summary`)
}

export async function getCallRecords(
  sessionId: string,
  phone?: string,
  page = 1,
  pageSize = 50,
): Promise<PagedCallRecords> {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  })
  if (phone) {
    query.set('phone', phone)
  }

  return apiFetch<PagedCallRecords>(`/api/sessions/${sessionId}/results/calls?${query.toString()}`)
}

export function openProgressStream(
  sessionId: string,
  onProgress: (progress: ProgressEventPayload) => void,
  onError: (error: string) => void,
): EventSource {
  const stream = new EventSource(`${API_BASE_URL}/api/sessions/${sessionId}/progress`)

  const toNumber = (value: unknown): number => {
    const num = Number(value)
    return Number.isFinite(num) ? num : 0
  }

  const parseEvent = (raw: string): ProgressEventPayload => {
    const parsed = JSON.parse(raw) as Record<string, unknown>
    return {
      processed: toNumber(parsed.processed ?? parsed.Processed),
      total: toNumber(parsed.total ?? parsed.Total),
      status: String(parsed.status ?? parsed.Status ?? 'unknown'),
      error: (parsed.error ?? parsed.Error) as string | null | undefined,
      percent: toNumber(parsed.percent ?? parsed.Percent),
    }
  }

  stream.addEventListener('progress', (event) => {
    if (!(event instanceof MessageEvent)) {
      return
    }

    try {
      onProgress(parseEvent(event.data))
    } catch {
      onError('Не удалось разобрать сообщение SSE прогресса.')
    }
  })

  stream.onerror = () => {
    onError('SSE-соединение прервано.')
  }

  return stream
}
