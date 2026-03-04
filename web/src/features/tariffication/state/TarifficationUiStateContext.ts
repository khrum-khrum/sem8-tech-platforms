import { createContext, useContext } from 'react'
import type { UploadKind } from '../../../api'

export type UploadedFiles = Record<UploadKind, File | null>

export const emptyUploadedFiles: UploadedFiles = {
  cdr: null,
  tariff: null,
  subscribers: null,
}

export type TarifficationUiState = {
  uploadedFiles: UploadedFiles
  resetVersion: number
  setUploadedFile: (kind: UploadKind, file: File) => void
  clearUploadedFile: (kind: UploadKind) => void
  resetUploadedFiles: () => void
}

export const TarifficationUiStateContext = createContext<TarifficationUiState | null>(null)

export function useTarifficationUiState() {
  const ctx = useContext(TarifficationUiStateContext)
  if (!ctx) {
    throw new Error('useTarifficationUiState must be used within TarifficationUiStateProvider')
  }
  return ctx
}

