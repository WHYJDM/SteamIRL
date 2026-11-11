import { useCallback, useEffect, useRef, useState } from 'react'
import { command, SessionError } from './api'
import type { GameSession } from './api'
import { useAuth } from '../accounts/AuthContext'

export function useSessionCommand() {
  const { setUser } = useAuth()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<SessionError | null>(null)
  const executing = useRef(false)
  const pending = useRef<{ signature: string; key: string } | null>(null)
  async function run(path: string, method: string, payload?: unknown): Promise<GameSession | null> {
    if (executing.current) return null
    const signature = JSON.stringify([path, method, payload])
    if (pending.current?.signature !== signature) pending.current = { signature, key: crypto.randomUUID() }
    executing.current = true; setBusy(true); setError(null)
    try { const response = await command(path, method, payload, pending.current.key); pending.current = null; return response }
    catch (failure) {
      const problem = failure instanceof SessionError ? failure : new SessionError('Не удалось выполнить действие.')
      setError(problem)
      if (problem.status === 401) setUser(null)
      // Network/5xx results are uncertain: retry the same logical command/key.
      if (problem.status > 0 && problem.status < 500) pending.current = null
      return null
    } finally { executing.current = false; setBusy(false) }
  }
  return { run, busy, error }
}

export function usePolling<T>(key: string, loader: (signal: AbortSignal) => Promise<T>) {
  const [data, setData] = useState<T>()
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [attempt, setAttempt] = useState(0)
  const refresh = useCallback(() => setAttempt(value => value + 1), [])
  useEffect(() => {
    let controller: AbortController
    const load = () => {
      controller?.abort(); controller = new AbortController()
      const signal = controller.signal
      loader(signal).then(value => { if (!signal.aborted) { setData(value); setError(''); setLoading(false) } })
        .catch((failure: unknown) => { if (!signal.aborted) { setError(failure instanceof Error ? failure.message : 'Не удалось обновить данные.'); setLoading(false) } })
    }
    setLoading(true); load()
    const timer = window.setInterval(load, 30000)
    window.addEventListener('focus', load)
    return () => { controller?.abort(); window.clearInterval(timer); window.removeEventListener('focus', load) }
  }, [key, loader, attempt])
  return { data, error, loading, refresh }
}

export function useServerNow(serverNow?: string) {
  const [, setTick] = useState(0)
  const reference = useRef({ server: serverNow, received: Date.now() })
  if (reference.current.server !== serverNow) reference.current = { server: serverNow, received: Date.now() }
  useEffect(() => { const timer = window.setInterval(() => setTick(value => value + 1), 1000); return () => window.clearInterval(timer) }, [])
  return serverNow ? Date.parse(serverNow) + Date.now() - reference.current.received : Date.now()
}
