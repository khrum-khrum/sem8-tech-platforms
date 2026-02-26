import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type { UploadTemplate } from '../uploadTemplates'

interface UploadScreenProps {
  template: UploadTemplate
  busy: boolean
  uploadStatus: string
  initialFile: File | null
  onBack: () => void
  onSubmit: (file: File) => void
}

export function UploadScreen({ template, busy, uploadStatus, initialFile, onBack, onSubmit }: UploadScreenProps) {
  const [file, setFile] = useState<File | null>(initialFile)

  useEffect(() => {
    setFile(initialFile)
  }, [initialFile, template.kind])

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!file) {
      return
    }
    onSubmit(file)
  }

  return (
    <section className="panel">
      <h2>{template.title}</h2>
      <p className="hint">{template.hint}</p>

      <form className="upload-form" onSubmit={handleSubmit}>
        <input
          type="file"
          accept={template.accept}
          onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          required
        />

        <div className="actions">
          <button type="button" onClick={onBack} disabled={busy}>
            В меню
          </button>
          <button type="submit" disabled={busy || !file}>
            {busy ? 'Сохранение...' : 'Сохранить файл'}
          </button>
        </div>
      </form>

      {uploadStatus ? <p className="info">{uploadStatus}</p> : null}
    </section>
  )
}
