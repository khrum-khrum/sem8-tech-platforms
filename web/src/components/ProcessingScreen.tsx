import type { ProgressEventPayload } from '../api'

interface ProcessingScreenProps {
  busy: boolean
  statusText: string
  progress: ProgressEventPayload | null
  onBack: () => void
}

export function ProcessingScreen({ busy, statusText, progress, onBack }: ProcessingScreenProps) {
  return (
    <section className="panel">
      <h2>Прогресс тарификации</h2>
      <p className="info">{statusText || 'Ожидание запуска...'}</p>
      <progress className="progress" max={100} value={Math.min(100, Math.max(0, progress?.percent ?? 0))} />
      <p className="hint">
        {progress ? `${progress.percent ?? 0}%` : '0%'} ({progress?.processed ?? 0}/{progress?.total ?? 0})
      </p>

      <div className="actions">
        <button onClick={onBack} disabled={busy}>
          В меню
        </button>
      </div>
    </section>
  )
}
