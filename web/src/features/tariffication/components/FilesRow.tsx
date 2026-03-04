import type { UploadKind } from '../../../api'
import type { UploadTemplate } from '../../../uploadTemplates'
import { FileUploadTile } from './FileUploadTile'

export function FilesRow(props: {
  templates: UploadTemplate[]
  values: Record<UploadKind, File | null>
  disabled: boolean
  onChange: (kind: UploadKind, file: File) => void
  onClear: (kind: UploadKind) => void
  onReset: () => void
  resetDisabled: boolean
  resetVersion: number
}) {
  return (
    <div className="files-row" role="group" aria-label="Загрузка файлов">
      {props.templates.map((template, idx) => (
        <FileUploadTile
          key={template.kind}
          template={template}
          gridColumn={idx + 1}
          value={props.values[template.kind]}
          disabled={props.disabled}
          onChange={(file) => props.onChange(template.kind, file)}
          onClear={() => props.onClear(template.kind)}
          resetVersion={props.resetVersion}
        />
      ))}

      <div className="files-reset" style={{ gridColumn: 4, gridRow: 2 }}>
        <button type="button" onClick={props.onReset} disabled={props.resetDisabled}>
          Сброс
        </button>
      </div>
    </div>
  )
}

