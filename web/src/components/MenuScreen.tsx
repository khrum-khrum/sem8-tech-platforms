import type { UploadKind } from '../api'
import type { UploadTemplate } from '../uploadTemplates'

interface MenuScreenProps {
  templates: UploadTemplate[]
  busy: boolean
  isRunning: boolean
  uploadedFileNames: Record<UploadKind, string | null>
  onSelect: (kind: UploadKind) => void
  onClearOne: (kind: UploadKind) => void
  onClearAll: () => void
  onStart: () => Promise<void>
}

export function MenuScreen({
  templates,
  busy,
  isRunning,
  uploadedFileNames,
  onSelect,
  onClearOne,
  onClearAll,
  onStart,
}: MenuScreenProps) {
  const readyToStart = templates.every((item) => Boolean(uploadedFileNames[item.kind]))

  return (
    <section className="panel">
      <h2>Выберите действие</h2>
      <div className="menu-grid">
        {templates.map((item) => (
          <div key={item.kind} className="menu-item">
            <button onClick={() => onSelect(item.kind)} disabled={busy || isRunning}>
              {item.title}
            </button>
            <p className="hint">
              {uploadedFileNames[item.kind] ? `Выбран: ${uploadedFileNames[item.kind]}` : 'Файл не выбран'}
            </p>
            <button onClick={() => onClearOne(item.kind)} disabled={busy || isRunning || !uploadedFileNames[item.kind]}>
              Очистить файл
            </button>
          </div>
        ))}
      </div>

      <div className="actions">
        <button onClick={() => void onStart()} disabled={busy || isRunning || !readyToStart}>
          {isRunning ? 'Выполняется...' : 'Старт тарификации'}
        </button>
        <button onClick={onClearAll} disabled={busy || isRunning}>
          Очистить все файлы
        </button>
      </div>
    </section>
  )
}
