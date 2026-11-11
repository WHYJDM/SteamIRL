import { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { getSession, logout } from './api'
import type { Account } from './api'

interface AuthState {
  user: Account | null | undefined
  error: string
  retry: () => void
  setUser: (user: Account | null) => void
  signOut: () => Promise<void>
}
const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<Account | null>()
  const [error, setError] = useState('')
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    getSession(controller.signal).then(session => { setUser(session.user); setError('') }).catch((failure: unknown) => {
      if (!controller.signal.aborted) setError(failure instanceof Error ? failure.message : 'Не удалось проверить вход.')
    })
    return () => controller.abort()
  }, [attempt])
  return <AuthContext.Provider value={{ user, error, retry: () => { setError(''); setAttempt(value => value + 1) }, setUser,
    signOut: async () => { await logout(); setUser(null) } }}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('AuthProvider is required')
  return context
}
