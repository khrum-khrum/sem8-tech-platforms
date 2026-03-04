import { useMemo, useRef, useState } from 'react'
import type { UploadKind } from '../../../api'
import type { UploadTemplate } from '../../../uploadTemplates'
import trashIconUrl from '../../../assets/icons/trash.png'

const shortTitles: Record<UploadKind, string> = {
  cdr: 'CDR',
  tariff: 'Тарифы',
  subscribers: 'Абоненты',
}

export function FileUploadTile(props: {
  template: UploadTemplate
  gridColumn: number
  resetVersion: number
  value: File | null
  disabled: boolean
  onChange: (file: File) => void
  onClear: () => void
}) {
  const [inputKey, setInputKey] = useState(0)
  const inputRef = useRef<HTMLInputElement | null>(null)

  const title = shortTitles[props.template.kind]
  const canClear = Boolean(props.value) && !props.disabled

  const accept = useMemo(() => props.template.accept, [props.template.accept])
  const fileLabel = props.value?.name ?? 'Нет файла'

  return (
    <>
      <div
        className="upload-meta"
        style={{ gridColumn: props.gridColumn, gridRow: 1 }}
        aria-label={props.template.title}
      >
        <h3 className="upload-title">{title}</h3>
        <p className="hint upload-hint">{props.template.hint}</p>
      </div>

      <div className="upload-control" style={{ gridColumn: props.gridColumn, gridRow: 2 }}>
        <input
          ref={inputRef}
          key={`${props.resetVersion}-${inputKey}`}
          className="upload-input-native"
          type="file"
          accept={accept}
          disabled={props.disabled}
          onChange={(event) => {
            const file = event.target.files?.[0]
            if (file) {
              props.onChange(file)
            }
          }}
        />

        <button
          type="button"
          className="upload-pick"
          disabled={props.disabled}
          onClick={() => inputRef.current?.click()}
        >
          Выбрать
        </button>

        <span className="upload-filename" title={props.value?.name ?? ''}>
          {fileLabel}
        </span>

        <button
          type="button"
          className="icon-button icon-button--small"
          onClick={() => {
            props.onClear()
            setInputKey((v) => v + 1)
          }}
          disabled={!canClear}
          aria-label="Удалить файл"
          title="Удалить файл"
        >
          <img className="icon-button-img" src={trashIconUrl} width={18} height={18} alt="" aria-hidden="true" />
        </button>
      </div>
    </>
  )
}

