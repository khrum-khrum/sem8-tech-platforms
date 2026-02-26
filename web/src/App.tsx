import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import './App.css'
import {
  createSession,
  getCallRecords,
  getSummary,
  openProgressStream,
  runTariffication,
  uploadFile,
} from './api'
import type { PagedCallRecords, ProgressEventPayload, SubscriberSummary, UploadKind } from './api'
import { CallRecordsSection } from './components/CallRecordsSection'
import { ErrorPanel } from './components/ErrorPanel'
import { MenuScreen } from './components/MenuScreen'
import { ProcessingScreen } from './components/ProcessingScreen'
import { SummarySection } from './components/SummarySection'
import { UploadScreen } from './components/UploadScreen'
import { uploadTemplates } from './uploadTemplates'

type Screen = 'menu' | 'upload' | 'processing'
type UploadedFiles = Record<UploadKind, File | null>

const emptyUploadedFiles: UploadedFiles = {
  cdr: null,
  tariff: null,
  subscribers: null,
}

function App() {
  const [screen, setScreen] = useState<Screen>('menu')
  const [sessionId, setSessionId] = useState<string | null>(null)
  const [selectedUploadKind, setSelectedUploadKind] = useState<UploadKind>('cdr')

  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [uploadStatus, setUploadStatus] = useState('')
  const [processingStatus, setProcessingStatus] = useState('')
  const [uploadedFiles, setUploadedFiles] = useState<UploadedFiles>(emptyUploadedFiles)

  const [progress, setProgress] = useState<ProgressEventPayload | null>(null)
  const [summary, setSummary] = useState<SubscriberSummary[]>([])
  const [callRecords, setCallRecords] = useState<PagedCallRecords | null>(null)
  const [phoneFilter, setPhoneFilter] = useState('')
  const [isRunning, setIsRunning] = useState(false)

  const streamRef = useRef<EventSource | null>(null)

  const selectedTemplate = useMemo(
    () => uploadTemplates.find((item) => item.kind === selectedUploadKind) ?? uploadTemplates[0],
    [selectedUploadKind],
  )

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
    return newSessionId
  }, [])

  const fetchResults = useCallback(
    async (targetSessionId: string, phone?: string) => {
      const [summaryResult, callsResult] = await Promise.all([
        getSummary(targetSessionId),
        getCallRecords(targetSessionId, phone),
      ])
      setSummary(summaryResult)
      setCallRecords(callsResult)
    },
    [],
  )

  const startTarifficationFlow = async (activeSessionId: string) => {
    setError('')
    setProcessingStatus('')
    setProgress(null)
    setSummary([])
    setCallRecords(null)
    setScreen('processing')
    closeProgressStream()
    setIsRunning(true)
    streamRef.current = openProgressStream(
      activeSessionId,
      async (event) => {
        setProgress(event)
        setProcessingStatus(`Статус: ${event.status} (${event.processed}/${event.total})`)

        if (event.status === 'completed') {
          setIsRunning(false)
          closeProgressStream()
          try {
            await fetchResults(activeSessionId)
          } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось загрузить результаты.')
          }
        }

        if (event.status === 'failed') {
          setIsRunning(false)
          closeProgressStream()
          setError(event.error ?? 'Тарификация завершилась с ошибкой.')
        }
      },
      (streamError) => {
        setIsRunning(false)
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
      closeProgressStream()
    }
  }

  const handleUploadSubmit = (file: File) => {
    setError('')
    setUploadedFiles((prev) => ({ ...prev, [selectedTemplate.kind]: file }))
    setUploadStatus(`Файл сохранен: ${file.name}`)
    setScreen('menu')
  }

  const handleClearOne = (kind: UploadKind) => {
    setUploadedFiles((prev) => ({ ...prev, [kind]: null }))
    setError('')
    setUploadStatus('')
  }

  const handleClearAll = () => {
    setUploadedFiles({ ...emptyUploadedFiles })
    setError('')
    setUploadStatus('')
    setSessionId(null)
    setProgress(null)
    setProcessingStatus('')
    setSummary([])
    setCallRecords(null)
    setPhoneFilter('')
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
      setScreen('processing')

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

  const handleApplyPhoneFilter = async () => {
    if (!sessionId) {
      return
    }
    setBusy(true)
    setError('')
    try {
      await fetchResults(sessionId, phoneFilter.trim() || undefined)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ошибка загрузки результатов.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="layout">
      <header className="panel">
        <h1>Тарификация CDR</h1>
      </header>

      {screen === 'menu' && (
        <MenuScreen
          templates={uploadTemplates}
          busy={busy}
          isRunning={isRunning}
          uploadedFileNames={{
            cdr: uploadedFiles.cdr?.name ?? null,
            tariff: uploadedFiles.tariff?.name ?? null,
            subscribers: uploadedFiles.subscribers?.name ?? null,
          }}
          onSelect={(kind) => {
            setSelectedUploadKind(kind)
            setError('')
            setScreen('upload')
          }}
          onClearOne={handleClearOne}
          onClearAll={handleClearAll}
          onStart={handleStartTariffication}
        />
      )}

      {screen === 'upload' && (
        <UploadScreen
          template={selectedTemplate}
          busy={busy}
          uploadStatus={uploadStatus}
          initialFile={uploadedFiles[selectedTemplate.kind]}
          onBack={() => setScreen('menu')}
          onSubmit={handleUploadSubmit}
        />
      )}

      {screen === 'processing' && (
        <ProcessingScreen
          busy={busy}
          statusText={processingStatus}
          progress={progress}
          onBack={() => setScreen('menu')}
        />
      )}

      <SummarySection summary={summary} />

      <CallRecordsSection
        callRecords={callRecords}
        busy={busy}
        phoneFilter={phoneFilter}
        onPhoneFilterChange={setPhoneFilter}
        onApplyFilter={handleApplyPhoneFilter}
      />

      <ErrorPanel error={error} />
    </main>
  )
}

export default App
