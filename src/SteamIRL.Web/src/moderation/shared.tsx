import { useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { useAuth } from '../accounts/AuthContext'
import { command, ContributionError, actions } from './api'
import type { Decision } from './api'
import { sessionTime } from '../sessions/time'

export function Access({ children, moderator = false }: { children: ReactNode; moderator?: boolean }) {
  const { user, error, retry } = useAuth()
  if (error) return <section className="account-panel"><p role="alert">{error}</p><button className="retry" onClick={retry}>Проверить вход</button></section>
  if (user === undefined) return <p className="notice" role="status">Проверяем вход…</p>
  if (!user) return <section className="account-panel"><h1>Войди в аккаунт</h1><p>Обращения доступны зарегистрированным игрокам.</p><a className="primary-link" href="#/login">Войти</a></section>
  if (moderator && !user.isModerator) return <section className="account-panel"><h1>Нужны права модератора</h1><p>Очередь доступна только назначенным модераторам.</p><button className="retry" onClick={retry}>Проверить права</button><p><a href="#/contributions">Мои обращения</a></p></section>
  return children
}
export function useContributionCommand() {
  const { setUser, retry } = useAuth()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<ContributionError | null>(null)
  const executing = useRef(false)
  const pending = useRef<{ signature: string; key: string } | null>(null)
  async function run<T>(path: string, payload: unknown): Promise<T | null> {
    if (executing.current) return null
    const signature = JSON.stringify([path, payload])
    if (pending.current?.signature !== signature) pending.current = { signature, key: crypto.randomUUID() }
    executing.current = true; setBusy(true); setError(null)
    try { const result = await command<T>(path, payload, pending.current.key); pending.current = null; return result }
    catch (failure) {
      const problem = failure instanceof ContributionError ? failure : new ContributionError('Не удалось выполнить действие.')
      setError(problem)
      if (problem.status === 401) setUser(null)
      if (problem.status === 403) retry()
      if (problem.status > 0 && problem.status < 500) pending.current = null
      return null
    } finally { executing.current = false; setBusy(false) }
  }
  return { run, busy, error }
}
export function Failure({ error }: { error: ContributionError | null }) {
  return error && <div className="form-error" role="alert"><p>{error.message}</p>{Object.entries(error.fields).map(([field, messages]) => <p key={field}>{messages.join(' ')}</p>)}
    {error.existingSubmissionId && <a href={`#/submissions/${error.existingSubmissionId}`}>Открыть отправленное предложение</a>}
    {error.existingReportId && <a href={`#/reports/${error.existingReportId}`}>Открыть отправленную жалобу</a>}</div>
}
export function History({ decisions }: { decisions: Decision[] }) {
  return decisions.length > 0 && <section className="review-history"><h2>Решение модератора</h2>{decisions.map(item => <article key={item.id}><strong>{actions[item.action] ?? item.action}</strong><p>{item.reason}</p><p>{item.moderatorName} · {sessionTime(item.decidedAt)}</p>{item.affectedSessions > 0 && <p>Отменено активных игр: {item.affectedSessions}.</p>}</article>)}</section>
}
export function Paging({ page, hasMore, setPage }: { page: number; hasMore: boolean; setPage: (page: number) => void }) {
  return <nav className="review-links" aria-label="Страницы обращений"><button className="retry" disabled={page <= 1} onClick={() => setPage(page - 1)}>Назад</button><span>Страница {page}</span><button className="retry" disabled={!hasMore} onClick={() => setPage(page + 1)}>Далее</button></nav>
}
