export type CachedSession = {
  id: string
  createdAtMs: number
}

const storageKey = 'tp.tariffication.sessions'

function safeParse(value: string | null): CachedSession[] {
  if (!value) return []
  try {
    const parsed = JSON.parse(value) as unknown
    if (!Array.isArray(parsed)) return []
    return parsed
      .map((x) => x as Partial<CachedSession>)
      .filter((x) => typeof x.id === 'string' && x.id.length > 0)
      .map((x) => ({ id: x.id as string, createdAtMs: Number(x.createdAtMs ?? Date.now()) }))
  } catch {
    return []
  }
}

export function loadCachedSessions(): CachedSession[] {
  const items = safeParse(localStorage.getItem(storageKey))
  return items.sort((a, b) => (b.createdAtMs ?? 0) - (a.createdAtMs ?? 0))
}

export function addCachedSession(sessionId: string) {
  const now = Date.now()
  const items = loadCachedSessions().filter((s) => s.id !== sessionId)
  items.unshift({ id: sessionId, createdAtMs: now })
  localStorage.setItem(storageKey, JSON.stringify(items.slice(0, 200)))
}

