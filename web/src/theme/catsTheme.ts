const STORAGE_KEY = 'ui.theme.cats'

function safeReadStorage(key: string): string | null {
  try {
    return window.localStorage.getItem(key)
  } catch {
    return null
  }
}

function safeWriteStorage(key: string, value: string) {
  try {
    window.localStorage.setItem(key, value)
  } catch {
    // ignore
  }
}

export function getCatsThemeEnabled(): boolean {
  return safeReadStorage(STORAGE_KEY) === '1'
}

export function applyCatsTheme(enabled: boolean) {
  const root = document.documentElement
  if (enabled) {
    root.dataset.theme = 'cats'
  } else if (root.dataset.theme === 'cats') {
    delete root.dataset.theme
  }
}

export function setCatsThemeEnabled(enabled: boolean) {
  safeWriteStorage(STORAGE_KEY, enabled ? '1' : '0')
  applyCatsTheme(enabled)
}

