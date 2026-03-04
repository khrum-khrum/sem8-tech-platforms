import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  createSession,
  getCallRecords,
  getSummary,
  openProgressStream,
  runTariffication,
  uploadFile,
} from '../../api'
import type { UploadKind } from '../../api'
import { ErrorPanel } from '../../components/ErrorPanel'
import { uploadTemplates } from '../../uploadTemplates'
import { FilesRow } from './components/FilesRow'
import { SessionResultsPanel } from './results/SessionResultsPanel'
import type { ResultsModel } from './results/SessionResultsPanel'
import { useTarifficationUiState } from './state/TarifficationUiStateContext'
import { useTarifficationSessionState } from './state/TarifficationSessionContext'
import { addCachedSession } from '../history/sessionCache'

function formatDurationMs(ms: number) {
  const totalSec = Math.max(0, Math.floor(ms / 1000))
  const min = Math.floor(totalSec / 60)
  const sec = totalSec % 60
  const msPart = Math.max(0, ms % 1000)
  const mm = String(min).padStart(2, '0')
  const ss = String(sec).padStart(2, '0')
  const mmm = String(msPart).padStart(3, '0')
  return `${mm}:${ss}.${mmm}`
}

export function TarifficationFlow() {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [isRunning, setIsRunning] = useState(false)
  const [nowMs, setNowMs] = useState(() => Date.now())

  const streamRef = useRef<EventSource | null>(null)
  const { uploadedFiles, resetVersion, setUploadedFile, clearUploadedFile, resetUploadedFiles } =
    useTarifficationUiState()
  const {
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
    resetSession,
  } = useTarifficationSessionState()
  const readyToStart = useMemo(() => uploadTemplates.every((t) => Boolean(uploadedFiles[t.kind])), [uploadedFiles])

  const closeProgressStream = useCallback(() => {
    if (streamRef.current) {
      streamRef.current.close()
      streamRef.current = null
    }
  }, [])

  useEffect(() => closeProgressStream, [closeProgressStream])

  const createNewSession = useCallback(async (): Promise<string> => {
    const newSessionId = await createSession()
    setSessionId(newSessionId)
    addCachedSession(newSessionId)
    return newSessionId
  }, [setSessionId])

  const fetchResults = useCallback(async (targetSessionId: string, phone?: string) => {
    const [summaryResult, callsResult] = await Promise.all([
      getSummary(targetSessionId),
      getCallRecords(targetSessionId, phone),
    ])
    setSummary(summaryResult)
    setCallRecords(callsResult)
  }, [setCallRecords, setSummary])

  useEffect(() => {
    if (!sessionId || isRunning) return
    if (summary.length > 0 && callRecords) return
    void fetchResults(sessionId).catch((err) => {
      setError(err instanceof Error ? err.message : 'Не удалось загрузить результаты.')
    })
  }, [callRecords, fetchResults, isRunning, sessionId, summary.length])

  useEffect(() => {
    if (!sessionId || !startedAtMs || finishedAtMs) return
    const id = window.setInterval(() => setNowMs(Date.now()), 100)
    return () => window.clearInterval(id)
  }, [finishedAtMs, sessionId, startedAtMs])

  const startTarifficationFlow = async (activeSessionId: string) => {
    setError('')
    setProcessingStatus('')
    setProgress(null)
    setSummary([])
    setCallRecords(null)
    closeProgressStream()
    setIsRunning(true)
    setStartedAtMs(Date.now())
    setFinishedAtMs(null)
    streamRef.current = openProgressStream(
      activeSessionId,
      async (event) => {
        setProgress(event)
        setProcessingStatus(`Статус: ${event.status} (${event.processed}/${event.total})`)

        if (event.status === 'completed') {
          setIsRunning(false)
          setFinishedAtMs(Date.now())
          closeProgressStream()
          try {
            await fetchResults(activeSessionId)
          } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось загрузить результаты.')
          }
        }

        if (event.status === 'failed') {
          setIsRunning(false)
          setFinishedAtMs(Date.now())
          closeProgressStream()
          setError(event.error ?? 'Тарификация завершилась с ошибкой.')
        }
      },
      (streamError) => {
        setIsRunning(false)
        setFinishedAtMs(Date.now())
        setError(streamError)
        closeProgressStream()
      },
    )

    try {
      await runTariffication(activeSessionId)
      setProcessingStatus('Тарификация запущена, ожидаем события прогресса...')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Не удалось запустить тарификацию.')
      setIsRunning(false)
      setFinishedAtMs(Date.now())
      closeProgressStream()
    }
  }

  const handleClearOne = (kind: UploadKind) => {
    clearUploadedFile(kind)
    setError('')
  }

  const handleResetFiles = () => {
    resetUploadedFiles()
    setError('')
  }

  const handleNewSession = () => {
    closeProgressStream()
    setBusy(false)
    setIsRunning(false)
    setError('')
    resetSession()
    resetUploadedFiles()
  }

  const handleStartTariffication = async () => {
    const missing = uploadTemplates.filter((item) => !uploadedFiles[item.kind])
    if (missing.length > 0) {
      setError('Для запуска нужны все три файла: CDR, тарифы и абоненты.')
      return
    }

    setBusy(true)
    setError('')

    try {
      const activeSessionId = await createNewSession()
      setProcessingStatus('Загружаем выбранные файлы...')

      for (const template of uploadTemplates) {
        const file = uploadedFiles[template.kind]
        if (!file) {
          throw new Error(`Файл для ${template.title} не выбран.`)
        }
        setProcessingStatus(`Загрузка: ${template.title} (${file.name})`)
        await uploadFile(activeSessionId, template.kind, file)
      }

      await startTarifficationFlow(activeSessionId)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ошибка загрузки файлов/запуска обработки.')
      setIsRunning(false)
      closeProgressStream()
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <section className="tariff-main">
        <h2 className="tariff-title">Файлы</h2>
        <FilesRow
          templates={uploadTemplates}
          values={uploadedFiles}
          disabled={busy || isRunning}
          onChange={(kind, file) => {
            setError('')
            setUploadedFile(kind, file)
          }}
          onClear={handleClearOne}
          onReset={handleResetFiles}
          resetDisabled={busy || isRunning}
          resetVersion={resetVersion}
        />

        <div className="actions tariff-actions">
          <button onClick={() => void handleStartTariffication()} disabled={busy || isRunning || !readyToStart}>
            {isRunning ? 'Выполняется...' : 'Старт тарификации'}
          </button>
          {sessionId ? (
            <button type="button" onClick={handleNewSession} disabled={busy}>
              Новая сессия
            </button>
          ) : null}
        </div>

        {sessionId ? (
          <>
            <div className="tariff-sessionline">
              <span>
                Сессия: <strong>{sessionId}</strong>
              </span>
              <span>
                Время:{' '}
                <strong>
                  {startedAtMs
                    ? formatDurationMs((finishedAtMs ?? nowMs) - startedAtMs)
                    : formatDurationMs(0)}
                </strong>
              </span>
            </div>

            <div className="tariff-progress">
              <progress className="progress" max={100} value={Math.min(100, Math.max(0, progress?.percent ?? 0))} />
              <p className="hint tariff-progress-hint">
                {processingStatus || 'Ожидание запуска...'}{' '}
                {progress ? `— ${progress.percent ?? 0}% (${progress.processed ?? 0}/${progress.total ?? 0})` : null}
              </p>
            </div>
          </>
        ) : null}
      </section>

      {sessionId ? (
        <SessionResultsPanel
          sessionId={sessionId}
          busy={busy}
          model={
            {
              summary,
              setSummary,
              callRecords,
              setCallRecords,
              phoneFilter,
              setPhoneFilter,
            } satisfies ResultsModel
          }
        />
      ) : null}

      <ErrorPanel error={error} />
    </>
  )
}

