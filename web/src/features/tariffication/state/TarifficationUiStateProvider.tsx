import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { TarifficationUiStateContext, emptyUploadedFiles } from './TarifficationUiStateContext'
import type { TarifficationUiState, UploadedFiles } from './TarifficationUiStateContext'

export function TarifficationUiStateProvider(props: { children: ReactNode }) {
  const [uploadedFiles, setUploadedFiles] = useState<UploadedFiles>(emptyUploadedFiles)
  const [resetVersion, setResetVersion] = useState(0)

  const value = useMemo<TarifficationUiState>(
    () => ({
      uploadedFiles,
      resetVersion,
      setUploadedFile: (kind, file) => setUploadedFiles((prev) => ({ ...prev, [kind]: file })),
      clearUploadedFile: (kind) => setUploadedFiles((prev) => ({ ...prev, [kind]: null })),
      resetUploadedFiles: () => {
        setResetVersion((v) => v + 1)
        setUploadedFiles({ ...emptyUploadedFiles })
      },
    }),
    [uploadedFiles, resetVersion],
  )

  return <TarifficationUiStateContext.Provider value={value}>{props.children}</TarifficationUiStateContext.Provider>
}

